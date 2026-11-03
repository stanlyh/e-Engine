using Aprobaciones.Application;
using Aprobaciones.Application.Documentos;

namespace Aprobaciones.Api.Endpoints;

internal static class BandejasEndpoints
{
    public static IEndpointRouteBuilder MapBandejasEndpoints(this IEndpointRouteBuilder app)
    {
        var aprobaciones = app.MapGroup("/api/aprobaciones").RequireAuthorization(Roles.Aprobador);

        aprobaciones.MapGet("/pendientes", (BandejasService s, string? buscar, CancellationToken ct, int pagina = 1, int tamano = 20) =>
            s.PendientesAsync(buscar, pagina, tamano, ct));

        aprobaciones.MapGet("/resueltos", (BandejasService s, string? buscar, CancellationToken ct, int pagina = 1, int tamano = 20) =>
            s.ResueltosPorMiAsync(buscar, pagina, tamano, ct));

        app.MapGet("/api/tesoreria/bandeja", (BandejasService s, bool? procesados, string? buscar, CancellationToken ct, int pagina = 1, int tamano = 20) =>
            s.TesoreriaAsync(procesados == true, buscar, pagina, tamano, ct))
            .RequireAuthorization(Roles.Tesoreria);

        return app;
    }
}
