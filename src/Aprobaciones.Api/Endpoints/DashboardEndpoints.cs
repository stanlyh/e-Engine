using Aprobaciones.Application.Documentos;

namespace Aprobaciones.Api.Endpoints;

internal static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        // ?todos=true (Admin) · ?desde= fecha de envío mínima
        app.MapGet("/api/dashboard/indicadores", (DashboardService s, bool? todos, DateTime? desde, CancellationToken ct) =>
            s.ObtenerAsync(todos == true, desde?.ToUniversalTime(), ct));

        return app;
    }
}
