using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Application.Documentos;
using Aprobaciones.Application.Notificaciones;
using Aprobaciones.Domain.Notificaciones;
using Microsoft.EntityFrameworkCore;

namespace Aprobaciones.Application.Administracion;

/// <summary>Seguimiento de la cola de correos desde Administración.</summary>
public sealed class NotificacionesService(
    IAprobacionesDbContext db,
    IUsuarioActual usuario,
    IEnviadorCorreo enviador,
    Notificador notificador,
    TimeProvider reloj)
{
    public async Task<ResumenCorreoDto> ResumenAsync(CancellationToken ct)
    {
        ExigirAdmin();
        var conteos = await db.Notificaciones.AsNoTracking()
            .GroupBy(n => n.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Estado, x => x.Cantidad, ct);
        var correo = await db.Usuarios.AsNoTracking().Where(u => u.Id == usuario.Id).Select(u => u.Email).SingleAsync(ct);
        return new ResumenCorreoDto(
            enviador.Modo,
            conteos.GetValueOrDefault(EstadoNotificacion.Pendiente),
            conteos.GetValueOrDefault(EstadoNotificacion.Enviada),
            conteos.GetValueOrDefault(EstadoNotificacion.Fallida) + conteos.GetValueOrDefault(EstadoNotificacion.SinDestinatarios),
            correo);
    }

    public async Task<Pagina<NotificacionDto>> ListarAsync(IReadOnlyCollection<EstadoNotificacion>? estados, int pagina, CancellationToken ct)
    {
        ExigirAdmin();
        var consulta = db.Notificaciones.AsNoTracking();
        if (estados is { Count: > 0 }) consulta = consulta.Where(n => estados.Contains(n.Estado));
        return await consulta
            .OrderByDescending(n => n.Id)
            .Select(n => new NotificacionDto(
                n.Id, n.Evento, n.Estado, n.Asunto,
                n.DocumentoId,
                db.Documentos.Where(d => d.Id == n.DocumentoId).Select(d => d.Folio).FirstOrDefault(),
                n.GrupoAd ?? db.Usuarios.Where(u => u.Id == n.UsuarioId).Select(u => u.Nombre).FirstOrDefault(),
                n.GrupoAd != null,
                n.Destinatarios, n.Intentos, n.FechaCreacion, n.ProximoIntento, n.FechaEnvio, n.UltimoError))
            .PaginarAsync(pagina, 20, ct);
    }

    public async Task ReintentarAsync(long id, CancellationToken ct)
    {
        ExigirAdmin();
        var notificacion = await db.Notificaciones.SingleOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new NoEncontradoException($"La notificación {id} no existe.");
        notificacion.Reintentar(reloj.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
    }

    public async Task<long> EnviarPruebaAsync(CancellationToken ct)
    {
        ExigirAdmin();
        var nombre = await db.Usuarios.AsNoTracking().Where(u => u.Id == usuario.Id).Select(u => u.Nombre).SingleAsync(ct);
        var notificacion = notificador.CrearPrueba(nombre);
        await db.SaveChangesAsync(ct);
        return notificacion.Id;
    }

    private void ExigirAdmin()
    {
        if (!usuario.EnRol(Roles.Admin)) throw new AccesoDenegadoException("Solo Administración puede ver los correos.");
    }
}

/// <param name="ConProblemas">Fallidas más sin destinatarios.</param>
public sealed record ResumenCorreoDto(string Modo, int Pendientes, int Enviadas, int ConProblemas, string? MiCorreo);

/// <param name="Destino">Grupo de AD o nombre del usuario.</param>
public sealed record NotificacionDto(
    long Id, EventoNotificacion Evento, EstadoNotificacion Estado, string Asunto, int? DocumentoId, string? Folio,
    string? Destino, bool EsGrupo, string? Destinatarios, int Intentos,
    DateTime FechaCreacion, DateTime ProximoIntento, DateTime? FechaEnvio, string? UltimoError);
