using System.Data.Common;
using System.Reflection;
using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Domain.Notificaciones;
using Aprobaciones.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Aprobaciones.Api.Endpoints;

internal static class SaludEndpoints
{
    /// <summary>Versión publicada (InformationalVersion del ensamblado; incluye el commit si se compila con SourceRevisionId).</summary>
    private static readonly string Version =
        typeof(SaludEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "desconocida";

    public static IEndpointRouteBuilder MapSaludEndpoints(this IEndpointRouteBuilder app)
    {
        // Para verificar un despliegue: base de datos, migraciones aplicadas y estado del envío de correos.
        // 200 = listo; 503 = falta algo (base de datos inaccesible o migraciones sin aplicar).
        app.MapGet("/api/salud", async (AprobacionesDbContext db, IEnviadorCorreo correo, CancellationToken ct) =>
        {
            try
            {
                var pendientes = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
                if (pendientes.Count > 0)
                    return Results.Json(new SaludDto("migraciones-pendientes", Version, true, pendientes, correo.Modo, null, null), statusCode: StatusCodes.Status503ServiceUnavailable);

                var cola = await db.Notificaciones.AsNoTracking()
                    .GroupBy(n => n.Estado)
                    .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
                    .ToDictionaryAsync(x => x.Estado, x => x.Cantidad, ct);
                return Results.Ok(new SaludDto(
                    "ok", Version, true, [], correo.Modo,
                    cola.GetValueOrDefault(EstadoNotificacion.Pendiente),
                    cola.GetValueOrDefault(EstadoNotificacion.Fallida) + cola.GetValueOrDefault(EstadoNotificacion.SinDestinatarios)));
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                return Results.Json(new SaludDto("sin-base-de-datos", Version, false, [], correo.Modo, null, null), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        return app;
    }
}

/// <param name="CorreosPendientes">null si no se pudo consultar.</param>
internal sealed record SaludDto(
    string Estado, string Version, bool BaseDeDatos, IReadOnlyList<string> MigracionesPendientes,
    string Correo, int? CorreosPendientes, int? CorreosConProblemas);
