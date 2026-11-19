namespace Aprobaciones.Application.Abstracciones;

/// <summary>Consulta de usuarios y grupos en Active Directory.</summary>
public interface IDirectorioUsuarios
{
    /// <param name="cuenta">DOMINIO\usuario</param>
    /// <returns>null si no se encuentra o el directorio no está disponible.</returns>
    DatosDirectorio? Buscar(string cuenta);

    /// <summary>Miembros (directos e indirectos) de un grupo; se usa para avisar a los aprobadores de un paso.</summary>
    ResultadoGrupo MiembrosDeGrupo(string grupoAd);
}

public sealed record DatosDirectorio(string Nombre, string? Email);

public sealed record MiembroDirectorio(string Cuenta, string Nombre, string? Email);

/// <param name="DirectorioDisponible">false si no se pudo consultar AD (p. ej. equipo fuera de dominio).</param>
public sealed record ResultadoGrupo(bool DirectorioDisponible, bool Existe, IReadOnlyList<MiembroDirectorio> Miembros)
{
    public static ResultadoGrupo NoDisponible { get; } = new(false, false, []);
    public static ResultadoGrupo Inexistente { get; } = new(true, false, []);
}
