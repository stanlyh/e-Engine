using Aprobaciones.Application.Abstracciones;
using Microsoft.EntityFrameworkCore;

namespace Aprobaciones.Api.Endpoints;

internal static class ReglasEndpoints
{
    public static IEndpointRouteBuilder MapReglasEndpoints(this IEndpointRouteBuilder app)
    {
        // Lectura para todos: el usuario ve qué cadena de aprobación le tocará según el monto.
        app.MapGet("/api/reglas-aprobacion", async (IAprobacionesDbContext db) =>
            await db.ReglasAprobacion
                .AsNoTracking()
                .OrderBy(r => r.MontoDesde)
                .Select(r => new ReglaDto(
                    r.Id, r.Nombre, r.MontoDesde, r.MontoHasta, r.Activa,
                    r.Pasos.OrderBy(p => p.Orden).Select(p => new ReglaPasoDto(p.Orden, p.Nombre, p.GrupoAd)).ToList()))
                .ToListAsync());

        return app;
    }
}

internal sealed record ReglaDto(int Id, string Nombre, decimal MontoDesde, decimal? MontoHasta, bool Activa, List<ReglaPasoDto> Pasos);

internal sealed record ReglaPasoDto(int Orden, string Nombre, string GrupoAd);
