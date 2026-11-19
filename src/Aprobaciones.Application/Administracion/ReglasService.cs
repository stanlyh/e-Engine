using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Domain;
using Aprobaciones.Domain.Reglas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aprobaciones.Application.Administracion;

/// <summary>
/// Edición de los rangos de aprobación. Se guarda la tabla completa de una vez porque la validez es del conjunto
/// (sin huecos ni traslapes); editar regla por regla obligaría a pasar por estados inválidos.
/// Los documentos ya enviados conservan la cadena con la que se enviaron.
/// </summary>
public sealed class ReglasService(
    IAprobacionesDbContext db,
    IUsuarioActual usuario,
    IDirectorioUsuarios directorio,
    TimeProvider reloj,
    ILogger<ReglasService> logger)
{
    public async Task<ConjuntoReglasDto> ObtenerAsync(CancellationToken ct)
    {
        ExigirAdmin();
        var reglas = await CargarAsync(asNoTracking: true, ct);
        var ids = reglas.Where(r => r.ModificadoPorId is not null).Select(r => r.ModificadoPorId!.Value).Distinct().ToList();
        var nombres = await db.Usuarios.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);

        return new ConjuntoReglasDto(
            Version(reglas),
            [.. reglas.Select(r => new ReglaAdminDto(
                r.Id, r.Nombre, r.MontoDesde, r.MontoHasta, r.Activa,
                [.. r.Pasos.OrderBy(p => p.Orden).Select(p => new PasoAdminDto(p.Nombre, p.GrupoAd))],
                r.ModificadoPorId is { } m ? nombres.GetValueOrDefault(m) : null,
                r.FechaModificacion))]);
    }

    public async Task<ConjuntoReglasDto> GuardarAsync(GuardarReglasRequest request, CancellationToken ct)
    {
        ExigirAdmin();
        var reglas = await CargarAsync(asNoTracking: false, ct);
        if (request.Version != Version(reglas))
            throw new ConflictoException("Otra persona modificó las reglas mientras las editabas. Recarga la página para ver los cambios.");

        var idsEnviados = request.Reglas.Where(r => r.Id is not null).Select(r => r.Id!.Value).ToList();
        if (idsEnviados.Except(reglas.Select(r => r.Id)).FirstOrDefault() is var desconocido && desconocido != 0)
            throw new DomainException($"La regla {desconocido} no existe.");
        if (idsEnviados.Count != idsEnviados.Distinct().Count())
            throw new DomainException("Una regla viene repetida.");

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var resultado = new List<ReglaAprobacion>();
        var cambios = 0;
        for (var i = 0; i < request.Reglas.Count; i++)
        {
            var datos = request.Reglas[i];
            List<(string, string)> pasos = [.. (datos.Pasos ?? []).Select(p => (p.Nombre ?? "", p.GrupoAd ?? ""))];
            try
            {
                if (datos.Id is { } id)
                {
                    var existente = reglas.Single(r => r.Id == id);
                    if (existente.Actualizar(datos.Nombre ?? "", datos.MontoDesde, datos.MontoHasta, datos.Activa, pasos, usuario.Id, ahora)) cambios++;
                    resultado.Add(existente);
                }
                else
                {
                    var nueva = new ReglaAprobacion(datos.Nombre ?? "", datos.MontoDesde, datos.MontoHasta);
                    foreach (var (nombre, grupo) in pasos) nueva.AgregarPaso(nombre, grupo);
                    if (!datos.Activa) nueva.Actualizar(nueva.Nombre, nueva.MontoDesde, nueva.MontoHasta, false, pasos, usuario.Id, ahora);
                    nueva.RegistrarAlta(usuario.Id, ahora);
                    db.ReglasAprobacion.Add(nueva);
                    resultado.Add(nueva);
                    cambios++;
                }
            }
            catch (DomainException ex)
            {
                throw new DomainException($"Regla {i + 1}{(string.IsNullOrWhiteSpace(datos.Nombre) ? "" : $" ('{datos.Nombre.Trim()}')")}: {ex.Message}");
            }
        }

        var eliminadas = reglas.Where(r => !idsEnviados.Contains(r.Id)).ToList();
        db.ReglasAprobacion.RemoveRange(eliminadas);
        cambios += eliminadas.Count;

        var errores = ReglaAprobacion.ValidarConjunto(resultado);
        if (errores.Count > 0) throw new DomainException(string.Join("\n", errores));

        if (cambios > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "{Cuenta} actualizó las reglas de aprobación: {Cambios} cambios, {Eliminadas} eliminadas. Activas: {Resumen}",
                usuario.Cuenta, cambios, eliminadas.Count,
                string.Join(" | ", resultado.Where(r => r.Activa).OrderBy(r => r.MontoDesde)
                    .Select(r => $"{r.MontoDesde:N2}-{r.MontoHasta?.ToString("N2") ?? "∞"}: {string.Join(" → ", r.Pasos.OrderBy(p => p.Orden).Select(p => p.GrupoAd))}")));
        }
        return await ObtenerAsync(ct);
    }

    /// <summary>Para el botón "Verificar" de cada paso: confirma que el grupo existe y quiénes recibirán los avisos.</summary>
    public MiembrosGrupoDto Miembros(string grupoAd)
    {
        ExigirAdmin();
        if (string.IsNullOrWhiteSpace(grupoAd)) throw new DomainException("Indica el grupo.");
        var resultado = directorio.MiembrosDeGrupo(grupoAd.Trim());
        return new MiembrosGrupoDto(
            grupoAd.Trim(), resultado.DirectorioDisponible, resultado.Existe,
            [.. resultado.Miembros.OrderBy(m => m.Nombre).Select(m => new MiembroDto(m.Cuenta, m.Nombre, m.Email))]);
    }

    private async Task<List<ReglaAprobacion>> CargarAsync(bool asNoTracking, CancellationToken ct)
    {
        var consulta = db.ReglasAprobacion.Include(r => r.Pasos).AsQueryable();
        if (asNoTracking) consulta = consulta.AsNoTracking();
        return await consulta.OrderBy(r => r.MontoDesde).ThenBy(r => r.Id).ToListAsync(ct);
    }

    /// <summary>Huella del contenido actual: si cambia entre leer y guardar, otra persona editó las reglas.</summary>
    private static string Version(IEnumerable<ReglaAprobacion> reglas)
    {
        var contenido = reglas.OrderBy(r => r.Id).Select(r => new
        {
            r.Id, r.Nombre, r.MontoDesde, r.MontoHasta, r.Activa,
            Pasos = r.Pasos.OrderBy(p => p.Orden).Select(p => new { p.Nombre, p.GrupoAd }),
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(contenido))))[..16];
    }

    private void ExigirAdmin()
    {
        if (!usuario.EnRol(Roles.Admin)) throw new AccesoDenegadoException("Solo Administración puede editar las reglas de aprobación.");
    }
}

public sealed record ConjuntoReglasDto(string Version, IReadOnlyList<ReglaAdminDto> Reglas);

public sealed record ReglaAdminDto(
    int Id, string Nombre, decimal MontoDesde, decimal? MontoHasta, bool Activa, IReadOnlyList<PasoAdminDto> Pasos,
    string? ModificadoPor, DateTime? FechaModificacion);

public sealed record PasoAdminDto(string Nombre, string GrupoAd);

public sealed record GuardarReglasRequest(string Version, IReadOnlyList<ReglaEdicion> Reglas);

/// <param name="Id">null = regla nueva. Las reglas existentes que no vengan se eliminan.</param>
public sealed record ReglaEdicion(int? Id, string? Nombre, decimal MontoDesde, decimal? MontoHasta, bool Activa, IReadOnlyList<PasoEdicion>? Pasos);

public sealed record PasoEdicion(string? Nombre, string? GrupoAd);

public sealed record MiembrosGrupoDto(string Grupo, bool DirectorioDisponible, bool Existe, IReadOnlyList<MiembroDto> Miembros);

public sealed record MiembroDto(string Cuenta, string Nombre, string? Email);
