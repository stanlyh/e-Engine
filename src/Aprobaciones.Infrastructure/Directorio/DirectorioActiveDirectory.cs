using System.DirectoryServices.AccountManagement;
using System.Security.Principal;
using Aprobaciones.Application.Abstracciones;
using Microsoft.Extensions.Logging;

namespace Aprobaciones.Infrastructure.Directorio;

internal sealed class DirectorioActiveDirectory(ILogger<DirectorioActiveDirectory> logger) : IDirectorioUsuarios
{
    public DatosDirectorio? Buscar(string cuenta)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var contexto = new PrincipalContext(ContextType.Domain);
            using var usuario = UserPrincipal.FindByIdentity(contexto, IdentityType.SamAccountName, cuenta);
            return usuario is null ? null : new DatosDirectorio(usuario.DisplayName ?? cuenta, usuario.EmailAddress);
        }
        catch (Exception ex) when (EsFallaDeDirectorio(ex))
        {
            // Equipo fuera de dominio (p. ej. desarrollo): se usa la cuenta como nombre.
            logger.LogWarning(ex, "No se pudo consultar Active Directory para {Cuenta}", cuenta);
            return null;
        }
    }

    public ResultadoGrupo MiembrosDeGrupo(string grupoAd)
    {
        if (!OperatingSystem.IsWindows()) return ResultadoGrupo.NoDisponible;
        try
        {
            using var contexto = new PrincipalContext(ContextType.Domain);
            // Acepta DOMINIO\Grupo o solo el nombre.
            var nombre = grupoAd.Contains('\\') ? grupoAd[(grupoAd.IndexOf('\\') + 1)..] : grupoAd;
            using var grupo = GroupPrincipal.FindByIdentity(contexto, IdentityType.SamAccountName, nombre);
            if (grupo is null) return ResultadoGrupo.Inexistente;

            var miembros = new List<MiembroDirectorio>();
            // recursive: incluye a los miembros de grupos anidados.
            foreach (var principal in grupo.GetMembers(recursive: true))
            {
                using (principal)
                {
                    if (principal is not UserPrincipal { Enabled: not false } usuario) continue;
                    var cuenta = usuario.Sid?.Translate(typeof(NTAccount)).Value ?? usuario.SamAccountName;
                    miembros.Add(new MiembroDirectorio(cuenta, usuario.DisplayName ?? cuenta, usuario.EmailAddress));
                }
            }
            return new ResultadoGrupo(true, true, miembros);
        }
        catch (Exception ex) when (EsFallaDeDirectorio(ex))
        {
            logger.LogWarning(ex, "No se pudo consultar el grupo {Grupo} en Active Directory", grupoAd);
            return ResultadoGrupo.NoDisponible;
        }
    }

    private static bool EsFallaDeDirectorio(Exception ex) =>
        ex is PrincipalServerDownException or PrincipalOperationException or InvalidOperationException or IdentityNotMappedException;
}
