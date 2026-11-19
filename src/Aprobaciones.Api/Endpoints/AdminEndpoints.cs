using Aprobaciones.Api.Seguridad;
using Aprobaciones.Application;
using Aprobaciones.Application.Administracion;
using Aprobaciones.Domain.Notificaciones;
using Microsoft.Extensions.Caching.Memory;

namespace Aprobaciones.Api.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin").RequireAuthorization(Roles.Admin);

        grupo.MapGet("/reglas", (ReglasService s, CancellationToken ct) => s.ObtenerAsync(ct));

        // Reemplaza la tabla completa; Version evita pisar los cambios de otra persona (409).
        grupo.MapPut("/reglas", async (GuardarReglasRequest request, ReglasService s, IMemoryCache cache, CancellationToken ct) =>
        {
            var resultado = await s.GuardarAsync(request, ct);
            cache.Remove(TransformacionClaims.ClaveGruposAprobacion);
            return resultado;
        });

        grupo.MapGet("/grupos/miembros", (string grupo, ReglasService s) => s.Miembros(grupo));

        grupo.MapGet("/correos/resumen", (NotificacionesService s, CancellationToken ct) => s.ResumenAsync(ct));

        // ?estado=A&estado=B (p. ej. "con problemas" = Fallida + SinDestinatarios)
        grupo.MapGet("/correos", (NotificacionesService s, EstadoNotificacion[]? estado, CancellationToken ct, int pagina = 1) =>
            s.ListarAsync(estado, pagina, ct));

        grupo.MapPost("/correos/{id:long}/reintentar", async (long id, NotificacionesService s, CancellationToken ct) =>
        {
            await s.ReintentarAsync(id, ct);
            return Results.NoContent();
        });

        grupo.MapPost("/correos/prueba", async (NotificacionesService s, CancellationToken ct) =>
            Results.Ok(new { id = await s.EnviarPruebaAsync(ct) }));

        return app;
    }
}
