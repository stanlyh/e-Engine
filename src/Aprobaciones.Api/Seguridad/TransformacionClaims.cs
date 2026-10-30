using System.Security.Claims;
using System.Security.Principal;
using Aprobaciones.Application;
using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Domain.Documentos;
using Aprobaciones.Domain.Usuarios;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Aprobaciones.Api.Seguridad;

/// <summary>
/// Convierte la identidad de Windows en la identidad de la aplicación: Id local del usuario,
/// roles (Usuario, Aprobador, Tesoreria, Admin) y los grupos de aprobación a los que pertenece.
/// </summary>
internal sealed class TransformacionClaims(
    IAprobacionesDbContext db,
    IDirectorioUsuarios directorio,
    IMemoryCache cache,
    IOptions<OpcionesSeguridad> opciones,
    IHostEnvironment entorno,
    TimeProvider reloj,
    ILogger<TransformacionClaims> logger) : IClaimsTransformation
{
    private const string TipoAutenticacion = "Aprobaciones";
    private const string ClaveGruposAprobacion = "grupos-aprobacion";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true, Name: { } cuenta }) return principal;
        if (principal.Identities.Any(i => i.AuthenticationType == TipoAutenticacion)) return principal;

        var config = opciones.Value;
        var simulados = entorno.IsDevelopment() ? config.GruposSimulados : [];
        bool Pertenece(string? grupo) =>
            !string.IsNullOrWhiteSpace(grupo)
            && (simulados.Contains(grupo, StringComparer.OrdinalIgnoreCase) || EnGrupoWindows(principal, grupo));

        var identidad = new ClaimsIdentity(TipoAutenticacion, ClaimTypes.Name, ClaimTypes.Role);
        identidad.AddClaim(new Claim(TiposClaim.UsuarioId, (await ObtenerUsuarioIdAsync(cuenta)).ToString()));

        foreach (var grupo in (await GruposAprobacionAsync()).Where(Pertenece))
            identidad.AddClaim(new Claim(TiposClaim.Grupo, grupo));

        if (string.IsNullOrWhiteSpace(config.GrupoUsuarios) || Pertenece(config.GrupoUsuarios))
            identidad.AddClaim(new Claim(ClaimTypes.Role, Roles.Usuario));
        if (identidad.HasClaim(c => c.Type == TiposClaim.Grupo))
            identidad.AddClaim(new Claim(ClaimTypes.Role, Roles.Aprobador));
        if (Pertenece(config.GrupoTesoreria))
            identidad.AddClaim(new Claim(ClaimTypes.Role, Roles.Tesoreria));
        if (Pertenece(config.GrupoAdmin))
            identidad.AddClaim(new Claim(ClaimTypes.Role, Roles.Admin));

        var resultado = principal.Clone();
        resultado.AddIdentity(identidad);
        return resultado;
    }

    private bool EnGrupoWindows(ClaimsPrincipal principal, string grupo)
    {
        try
        {
            return principal.IsInRole(grupo);
        }
        catch (IdentityNotMappedException)
        {
            logger.LogWarning("El grupo {Grupo} no existe en el dominio", grupo);
            return false;
        }
    }

    /// <summary>Grupos que aprueban algún paso: reglas activas y pasos pendientes de documentos en curso.</summary>
    private async Task<IReadOnlyList<string>> GruposAprobacionAsync() =>
        await cache.GetOrCreateAsync(ClaveGruposAprobacion, async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var deReglas = db.ReglasAprobacion.Where(r => r.Activa).SelectMany(r => r.Pasos).Select(p => p.GrupoAd);
            var enCurso = db.DocumentoAprobaciones
                .Where(a => a.Estado == EstadoPaso.Pendiente || a.Estado == EstadoPaso.EnEspera)
                .Select(a => a.GrupoAd);
            return (IReadOnlyList<string>)await deReglas.Union(enCurso).ToListAsync();
        }) ?? [];

    /// <summary>Busca o registra al usuario en su primer acceso; refresca nombre y correo desde AD al expirar la caché.</summary>
    private async Task<int> ObtenerUsuarioIdAsync(string cuenta) =>
        await cache.GetOrCreateAsync($"usuario:{cuenta.ToUpperInvariant()}", async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            var ahora = reloj.GetUtcNow().UtcDateTime;
            var datos = directorio.Buscar(cuenta);
            var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Cuenta == cuenta);

            if (usuario is null)
            {
                usuario = new Usuario(cuenta, datos?.Nombre ?? cuenta, datos?.Email, ahora);
                db.Usuarios.Add(usuario);
            }
            else
            {
                usuario.RegistrarAcceso(datos?.Nombre ?? usuario.Nombre, datos?.Email ?? usuario.Email, ahora);
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException) when (usuario.Id == 0)
            {
                // Dos peticiones simultáneas en el primer acceso: la otra ya lo creó.
                db.Usuarios.Entry(usuario).State = EntityState.Detached;
                return await db.Usuarios.Where(u => u.Cuenta == cuenta).Select(u => u.Id).SingleAsync();
            }
            return usuario.Id;
        });
}
