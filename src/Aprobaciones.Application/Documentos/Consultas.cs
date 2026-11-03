using Aprobaciones.Domain.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Aprobaciones.Application.Documentos;

internal static class Consultas
{
    /// <summary>Texto libre: UUID exacto, o coincidencia en folio, descripción, emisor o RFC.</summary>
    public static IQueryable<Documento> Buscar(this IQueryable<Documento> consulta, string? buscar)
    {
        if (string.IsNullOrWhiteSpace(buscar)) return consulta;
        var texto = buscar.Trim();
        return Guid.TryParse(texto, out var uuid)
            ? consulta.Where(d => d.Cfdi!.Uuid == uuid)
            : consulta.Where(d => d.Folio.Contains(texto) || d.Descripcion.Contains(texto)
                || d.Cfdi!.NombreEmisor.Contains(texto) || d.Cfdi.RfcEmisor.Contains(texto));
    }

    /// <param name="ordenada">La consulta ya ordenada; se cuenta antes de paginar.</param>
    public static async Task<Pagina<T>> PaginarAsync<T>(
        this IQueryable<T> ordenada, int numeroPagina, int tamanoPagina, CancellationToken ct)
    {
        numeroPagina = Math.Max(1, numeroPagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 100);
        var total = await ordenada.CountAsync(ct);
        var elementos = await ordenada.Skip((numeroPagina - 1) * tamanoPagina).Take(tamanoPagina).ToListAsync(ct);
        return new Pagina<T>(elementos, total, numeroPagina, tamanoPagina);
    }
}
