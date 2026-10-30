using System.DirectoryServices.AccountManagement;
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
        catch (Exception ex) when (ex is PrincipalServerDownException or PrincipalOperationException or InvalidOperationException)
        {
            // Equipo fuera de dominio (p. ej. desarrollo): se usa la cuenta como nombre.
            logger.LogWarning(ex, "No se pudo consultar Active Directory para {Cuenta}", cuenta);
            return null;
        }
    }
}
