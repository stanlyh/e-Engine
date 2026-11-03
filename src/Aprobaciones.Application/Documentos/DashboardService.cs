using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Domain.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Aprobaciones.Application.Documentos;

/// <summary>
/// Indicadores del inicio. Todos se cuentan sobre documentos ya enviados (FechaEnvio), del usuario actual
/// o de todos (solo Admin), opcionalmente desde una fecha de envío:
/// Enviados = todo lo enviado · Pendientes = en aprobación · Autorizados = pasaron toda la cadena
/// (en Tesorería + procesados) · Rechazados = esperando corrección · En Tesorería · Procesados.
/// </summary>
public sealed class DashboardService(IAprobacionesDbContext db, IUsuarioActual usuario, BandejasService bandejas)
{
    private const int MovimientosRecientes = 8;

    public async Task<DashboardDto> ObtenerAsync(bool todos, DateTime? enviadosDesde, CancellationToken ct)
    {
        if (todos && !usuario.EnRol(Roles.Admin))
            throw new AccesoDenegadoException("Solo Administración puede ver los indicadores de todos los documentos.");

        var uid = usuario.Id;
        var documentos = db.Documentos.AsNoTracking();
        if (!todos) documentos = documentos.Where(d => d.CreadoPorId == uid);

        var enviados = documentos.Where(d => d.FechaEnvio != null);
        if (enviadosDesde is { } desde) enviados = enviados.Where(d => d.FechaEnvio >= desde);

        var totales = await enviados
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Enviados = g.Count(),
                MontoEnviados = g.Sum(d => d.MontoMxn ?? 0),
                Pendientes = g.Count(d => d.Estado == EstadoDocumento.EnAprobacion),
                MontoPendientes = g.Sum(d => d.Estado == EstadoDocumento.EnAprobacion ? d.MontoMxn ?? 0 : 0),
                Autorizados = g.Count(d => d.Estado == EstadoDocumento.EnTesoreria || d.Estado == EstadoDocumento.Procesado),
                MontoAutorizados = g.Sum(d => d.Estado == EstadoDocumento.EnTesoreria || d.Estado == EstadoDocumento.Procesado ? d.MontoMxn ?? 0 : 0),
                Rechazados = g.Count(d => d.Estado == EstadoDocumento.Rechazado),
                MontoRechazados = g.Sum(d => d.Estado == EstadoDocumento.Rechazado ? d.MontoMxn ?? 0 : 0),
                EnTesoreria = g.Count(d => d.Estado == EstadoDocumento.EnTesoreria),
                MontoEnTesoreria = g.Sum(d => d.Estado == EstadoDocumento.EnTesoreria ? d.MontoMxn ?? 0 : 0),
                Procesados = g.Count(d => d.Estado == EstadoDocumento.Procesado),
                MontoProcesados = g.Sum(d => d.Estado == EstadoDocumento.Procesado ? d.MontoMxn ?? 0 : 0),
            })
            .SingleOrDefaultAsync(ct);

        var indicadores = totales is null
            ? new IndicadoresDto(new(0, 0), new(0, 0), new(0, 0), new(0, 0), new(0, 0), new(0, 0))
            : new IndicadoresDto(
                new(totales.Enviados, totales.MontoEnviados),
                new(totales.Pendientes, totales.MontoPendientes),
                new(totales.Autorizados, totales.MontoAutorizados),
                new(totales.Rechazados, totales.MontoRechazados),
                new(totales.EnTesoreria, totales.MontoEnTesoreria),
                new(totales.Procesados, totales.MontoProcesados));

        // Tarjetas de trabajo por rol: no dependen del alcance ni del periodo.
        int? esperanMiAprobacion = usuario.EnRol(Roles.Aprobador) ? await bandejas.ContarPendientesAsync(ct) : null;
        IndicadorDto? porProcesar = null;
        if (usuario.EnRol(Roles.Tesoreria))
        {
            var cola = db.Documentos.AsNoTracking().Where(d => d.Estado == EstadoDocumento.EnTesoreria);
            porProcesar = new IndicadorDto(await cola.CountAsync(ct), await cola.SumAsync(d => d.MontoMxn ?? 0, ct));
        }

        var movimientos = await db.DocumentoHistorial.AsNoTracking()
            .Where(h => h.Accion != AccionHistorial.ArchivoCargado && h.Accion != AccionHistorial.Editado)
            .Join(documentos, h => h.DocumentoId, d => d.Id, (h, d) => new { h, d })
            .OrderByDescending(x => x.h.Fecha)
            .ThenByDescending(x => x.h.Id)
            .Take(MovimientosRecientes)
            .Select(x => new MovimientoDto(
                x.d.Id, x.d.Folio, x.d.Descripcion, x.h.Accion, x.h.EstadoNuevo,
                db.Usuarios.Where(u => u.Id == x.h.UsuarioId).Select(u => u.Nombre).First(),
                x.h.Comentario, x.h.Fecha))
            .ToListAsync(ct);

        return new DashboardDto(todos, enviadosDesde, indicadores, esperanMiAprobacion, porProcesar, movimientos);
    }
}

public sealed record IndicadorDto(int Cantidad, decimal Monto);

public sealed record IndicadoresDto(
    IndicadorDto Enviados,
    IndicadorDto Pendientes,
    IndicadorDto Autorizados,
    IndicadorDto Rechazados,
    IndicadorDto EnTesoreria,
    IndicadorDto Procesados);

public sealed record MovimientoDto(
    int DocumentoId, string Folio, string Descripcion, AccionHistorial Accion, EstadoDocumento EstadoNuevo,
    string Usuario, string? Comentario, DateTime Fecha);

public sealed record DashboardDto(
    bool Todos,
    DateTime? EnviadosDesde,
    IndicadoresDto Indicadores,
    int? EsperanMiAprobacion,
    IndicadorDto? PorProcesar,
    IReadOnlyList<MovimientoDto> Movimientos);
