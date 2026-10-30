namespace Aprobaciones.Application;

/// <summary>Roles de la aplicación; se derivan de grupos de AD al autenticar.</summary>
public static class Roles
{
    public const string Usuario = "Usuario";
    /// <summary>Miembro de al menos un grupo que aprueba un paso de alguna regla.</summary>
    public const string Aprobador = "Aprobador";
    public const string Tesoreria = "Tesoreria";
    public const string Admin = "Admin";
}
