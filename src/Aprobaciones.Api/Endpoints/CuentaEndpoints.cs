using Aprobaciones.Api.Seguridad;
using Aprobaciones.Application.Abstracciones;
using Microsoft.EntityFrameworkCore;

namespace Aprobaciones.Api.Endpoints;

internal static class CuentaEndpoints
{
    public static IEndpointRouteBuilder MapCuentaEndpoints(this IEndpointRouteBuilder app)
    {
        // Angular lo llama al iniciar: identifica al usuario y entrega el token antiforgery.
        app.MapGet("/api/me", async (HttpContext contexto, IAprobacionesDbContext db, IUsuarioActual actual) =>
        {
            ValidacionAntiforgery.EmitirToken(contexto);
            var usuario = await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == actual.Id);
            return new UsuarioDto(usuario.Id, usuario.Cuenta, usuario.Nombre, usuario.Email, actual.Roles, actual.Grupos);
        });

        return app;
    }
}

internal sealed record UsuarioDto(
    int Id, string Cuenta, string Nombre, string? Email, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Grupos);
