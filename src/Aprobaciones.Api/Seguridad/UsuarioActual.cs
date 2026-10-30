using System.Security.Claims;
using Aprobaciones.Application.Abstracciones;

namespace Aprobaciones.Api.Seguridad;

internal sealed class UsuarioActual(IHttpContextAccessor accesor) : IUsuarioActual
{
    private ClaimsPrincipal Principal =>
        accesor.HttpContext?.User ?? throw new InvalidOperationException("No hay una petición HTTP activa.");

    public int Id => int.Parse(
        Principal.FindFirstValue(TiposClaim.UsuarioId) ?? throw new InvalidOperationException("El usuario no está identificado."));

    public string Cuenta => Principal.Identity?.Name ?? "";

    public IReadOnlyCollection<string> Roles =>
        [.. Principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct()];

    public IReadOnlyCollection<string> Grupos =>
        [.. Principal.FindAll(TiposClaim.Grupo).Select(c => c.Value)];

    public bool EnRol(string rol) => Principal.IsInRole(rol);

    public bool PerteneceAGrupo(string grupoAd) =>
        Principal.FindAll(TiposClaim.Grupo).Any(c => string.Equals(c.Value, grupoAd, StringComparison.OrdinalIgnoreCase));
}
