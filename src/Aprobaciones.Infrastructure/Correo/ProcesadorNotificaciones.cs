using System.Net.Mail;
using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Domain.Notificaciones;
using Aprobaciones.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aprobaciones.Infrastructure.Correo;

/// <summary>
/// Envía la cola de notificaciones cada pocos segundos. Cada notificación se reclama con un UPDATE
/// condicional antes de enviarla, así que dos instancias del sitio nunca envían el mismo correo.
/// </summary>
internal sealed class ProcesadorNotificaciones(
    IServiceScopeFactory scopes,
    IOptions<OpcionesCorreo> opciones,
    TimeProvider reloj,
    ILogger<ProcesadorNotificaciones> logger) : BackgroundService
{
    /// <summary>Si el proceso muere a medio envío, la notificación se libera después de este tiempo.</summary>
    private static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(1, opciones.Value.IntervaloSegundos));
        logger.LogInformation("Envío de correos activo en modo {Modo}, cada {Intervalo}", opciones.Value.Modo, intervalo);
        using var temporizador = new PeriodicTimer(intervalo, reloj);
        do
        {
            try
            {
                await ProcesarLoteAsync(detener);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Base de datos no disponible, etc.: se intenta de nuevo en el siguiente ciclo.
                logger.LogError(ex, "Error al procesar la cola de correos");
            }
        }
        while (await temporizador.WaitForNextTickAsync(detener));
    }

    internal async Task ProcesarLoteAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AprobacionesDbContext>();
        var directorio = scope.ServiceProvider.GetRequiredService<IDirectorioUsuarios>();
        var enviador = scope.ServiceProvider.GetRequiredService<IEnviadorCorreo>();

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var ids = await db.Notificaciones.AsNoTracking()
            .Where(n => n.Estado == EstadoNotificacion.Pendiente && n.ProximoIntento <= ahora
                && (n.BloqueadaHasta == null || n.BloqueadaHasta < ahora))
            .OrderBy(n => n.Id)
            .Select(n => n.Id)
            .Take(Math.Max(1, opciones.Value.Lote))
            .ToListAsync(ct);

        foreach (var id in ids)
        {
            ahora = reloj.GetUtcNow().UtcDateTime;
            var hasta = ahora + Bloqueo;
            var reclamada = await db.Notificaciones
                .Where(n => n.Id == id && n.Estado == EstadoNotificacion.Pendiente && (n.BloqueadaHasta == null || n.BloqueadaHasta < ahora))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.BloqueadaHasta, hasta), ct);
            if (reclamada == 0) continue;

            var notificacion = await db.Notificaciones.SingleAsync(n => n.Id == id, ct);
            await EnviarAsync(db, directorio, enviador, notificacion, ct);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    private async Task EnviarAsync(
        AprobacionesDbContext db, IDirectorioUsuarios directorio, IEnviadorCorreo enviador, Notificacion notificacion, CancellationToken ct)
    {
        var (destinatarios, problema) = await ResolverAsync(db, directorio, notificacion, ct);
        var ahora = reloj.GetUtcNow().UtcDateTime;
        if (destinatarios is null)
        {
            // Directorio no disponible: es temporal, se reintenta.
            notificacion.RegistrarFallo(problema!, [], ahora);
            logger.LogWarning("Notificación {Id}: {Problema}", notificacion.Id, problema);
            return;
        }
        if (destinatarios.Count == 0)
        {
            notificacion.MarcarSinDestinatarios(problema!, ahora);
            logger.LogWarning("Notificación {Id} sin destinatarios: {Problema}", notificacion.Id, problema);
            return;
        }

        try
        {
            await enviador.EnviarAsync(new MensajeCorreo(destinatarios, notificacion.Asunto, notificacion.CuerpoHtml, notificacion.CuerpoTexto), ct);
            notificacion.MarcarEnviada(destinatarios, reloj.GetUtcNow().UtcDateTime);
        }
        catch (Exception ex) when (ex is SmtpException or IOException or InvalidOperationException or FormatException or UnauthorizedAccessException)
        {
            notificacion.RegistrarFallo(ex.GetBaseException().Message, destinatarios, reloj.GetUtcNow().UtcDateTime);
            logger.LogWarning(ex, "No se pudo enviar la notificación {Id} (intento {Intento})", notificacion.Id, notificacion.Intentos);
        }
    }

    /// <returns>(null, motivo) si hay que reintentar; (vacía, motivo) si no hay a quién enviar.</returns>
    private static async Task<(IReadOnlyList<string>? Destinatarios, string? Problema)> ResolverAsync(
        AprobacionesDbContext db, IDirectorioUsuarios directorio, Notificacion notificacion, CancellationToken ct)
    {
        if (notificacion.UsuarioId is { } usuarioId)
        {
            var usuario = await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == usuarioId, ct);
            // El correo se copia de AD en cada acceso; si aún no lo tiene, se pregunta al directorio.
            var correo = usuario.Email ?? directorio.Buscar(usuario.Cuenta)?.Email;
            return correo is { Length: > 0 }
                ? ([correo], null)
                : ([], $"{usuario.Nombre} ({usuario.Cuenta}) no tiene correo registrado.");
        }

        var grupo = directorio.MiembrosDeGrupo(notificacion.GrupoAd!);
        if (!grupo.DirectorioDisponible) return (null, $"No se pudo consultar el grupo {notificacion.GrupoAd} en el directorio.");
        if (!grupo.Existe) return ([], $"El grupo {notificacion.GrupoAd} no existe en el directorio.");

        var excluidas = notificacion.CuentasExcluidas.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidatos = grupo.Miembros.Where(m => !excluidas.Contains(m.Cuenta)).ToList();
        var correos = candidatos.Where(m => !string.IsNullOrWhiteSpace(m.Email)).Select(m => m.Email!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (correos.Count > 0) return (correos, null);

        return ([], candidatos.Count == 0
            ? $"El grupo {notificacion.GrupoAd} no tiene a nadie más que pueda recibirla (se excluyó a quien cargó el documento y a quienes ya aprobaron)."
            : $"Ningún miembro del grupo {notificacion.GrupoAd} tiene correo registrado.");
    }
}
