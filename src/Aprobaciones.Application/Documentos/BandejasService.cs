using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Domain.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Aprobaciones.Application.Documentos;

/// <summary>Bandejas de trabajo: documentos que esperan al aprobador actual y documentos en Tesorería.</summary>
public sealed class BandejasService(IAprobacionesDbContext db, IUsuarioActual usuario)
{
    /// <summary>
    /// Documentos cuyo paso actual corresponde a un grupo del usuario y que puede resolver:
    /// no los cargó él y no aprobó ya otro paso de la misma ronda. Los que más tiempo llevan esperando, primero.
    /// </summary>
    public Task<Pagina<PendienteAprobacionDto>> PendientesAsync(string? buscar, int numeroPagina, int tamanoPagina, CancellationToken ct) =>
        ConsultaPendientes()
            .Buscar(buscar)
            .Select(d => new
            {
                Documento = d,
                Paso = d.Aprobaciones.Where(a => a.Ronda == d.Ronda && a.Estado == EstadoPaso.Pendiente).Select(a => new { a.Nombre, a.Orden }).First(),
                TotalPasos = d.Aprobaciones.Count(a => a.Ronda == d.Ronda),
                // Llegó al paso actual cuando se aprobó el anterior, o al enviarse si es el primero.
                EsperaDesde = d.Aprobaciones.Where(a => a.Ronda == d.Ronda && a.Estado == EstadoPaso.Aprobado).Max(a => a.FechaResolucion) ?? d.FechaEnvio,
                CreadoPor = db.Usuarios.Where(u => u.Id == d.CreadoPorId).Select(u => u.Nombre).First(),
            })
            .OrderBy(x => x.EsperaDesde)
            .Select(x => new PendienteAprobacionDto(
                x.Documento.Id, x.Documento.Folio, x.Documento.Descripcion, x.CreadoPor,
                x.Documento.Cfdi!.NombreEmisor, x.Documento.Cfdi.RfcEmisor, x.Documento.Cfdi.Total, x.Documento.Cfdi.Moneda, x.Documento.MontoMxn,
                x.Paso.Nombre, x.Paso.Orden, x.TotalPasos, x.EsperaDesde))
            .PaginarAsync(numeroPagina, tamanoPagina, ct);

    public Task<int> ContarPendientesAsync(CancellationToken ct) => ConsultaPendientes().CountAsync(ct);

    private IQueryable<Documento> ConsultaPendientes()
    {
        var uid = usuario.Id;
        var grupos = usuario.Grupos.ToList();
        return db.Documentos.AsNoTracking()
            .Where(d => d.Estado == EstadoDocumento.EnAprobacion && d.CreadoPorId != uid)
            .Where(d => d.Aprobaciones.Any(a => a.Ronda == d.Ronda && a.Estado == EstadoPaso.Pendiente && grupos.Contains(a.GrupoAd)))
            .Where(d => !d.Aprobaciones.Any(a => a.Ronda == d.Ronda && a.Estado == EstadoPaso.Aprobado && a.ResueltoPorId == uid));
    }

    /// <summary>Decisiones que tomó el usuario actual, de la más reciente a la más antigua.</summary>
    public Task<Pagina<ResueltoPorMiDto>> ResueltosPorMiAsync(string? buscar, int numeroPagina, int tamanoPagina, CancellationToken ct)
    {
        var uid = usuario.Id;
        var documentos = db.Documentos.AsNoTracking().Buscar(buscar);

        return db.DocumentoAprobaciones.AsNoTracking()
            .Where(a => a.ResueltoPorId == uid)
            .Join(documentos, a => a.DocumentoId, d => d.Id, (a, d) => new { a, d })
            .OrderByDescending(x => x.a.FechaResolucion)
            .Select(x => new ResueltoPorMiDto(
                x.d.Id, x.d.Folio, x.d.Descripcion, x.d.Cfdi!.NombreEmisor, x.d.Cfdi.Total, x.d.Cfdi.Moneda,
                x.a.Nombre, x.a.Estado, x.a.Comentario, x.a.FechaResolucion!.Value, x.d.Estado))
            .PaginarAsync(numeroPagina, tamanoPagina, ct);
    }

    /// <summary>
    /// Documentos autorizados que esperan a Tesorería (los más antiguos primero),
    /// o ya procesados (los más recientes primero).
    /// </summary>
    public Task<Pagina<DocumentoTesoreriaDto>> TesoreriaAsync(bool procesados, string? buscar, int numeroPagina, int tamanoPagina, CancellationToken ct)
    {
        if (!usuario.EnRol(Roles.Tesoreria)) throw new AccesoDenegadoException("Solo Tesorería puede ver esta bandeja.");

        var estado = procesados ? EstadoDocumento.Procesado : EstadoDocumento.EnTesoreria;
        var consulta = db.Documentos.AsNoTracking().Where(d => d.Estado == estado).Buscar(buscar);
        var ordenada = procesados
            ? consulta.OrderByDescending(d => d.FechaProcesado)
            : consulta.OrderBy(d => d.FechaAutorizacion);

        return ordenada
            .Select(d => new DocumentoTesoreriaDto(
                d.Id, d.Folio, d.Descripcion,
                db.Usuarios.Where(u => u.Id == d.CreadoPorId).Select(u => u.Nombre).First(),
                d.Cfdi!.NombreEmisor, d.Cfdi.RfcEmisor, d.Cfdi.Uuid, d.Cfdi.Total, d.Cfdi.Moneda, d.MontoMxn,
                d.FechaAutorizacion!.Value, d.FechaProcesado, d.ReferenciaTesoreria))
            .PaginarAsync(numeroPagina, tamanoPagina, ct);
    }
}

public sealed record PendienteAprobacionDto(
    int Id,
    string Folio,
    string Descripcion,
    string CreadoPor,
    string? NombreEmisor,
    string? RfcEmisor,
    decimal? Total,
    string? Moneda,
    decimal? MontoMxn,
    string PasoActual,
    int OrdenPaso,
    int TotalPasos,
    DateTime? EsperaDesde);

public sealed record ResueltoPorMiDto(
    int Id,
    string Folio,
    string Descripcion,
    string? NombreEmisor,
    decimal? Total,
    string? Moneda,
    string Paso,
    EstadoPaso MiDecision,
    string? Comentario,
    DateTime FechaResolucion,
    EstadoDocumento EstadoActual);

public sealed record DocumentoTesoreriaDto(
    int Id,
    string Folio,
    string Descripcion,
    string CreadoPor,
    string? NombreEmisor,
    string? RfcEmisor,
    Guid? Uuid,
    decimal? Total,
    string? Moneda,
    decimal? MontoMxn,
    DateTime FechaAutorizacion,
    DateTime? FechaProcesado,
    string? ReferenciaTesoreria);
