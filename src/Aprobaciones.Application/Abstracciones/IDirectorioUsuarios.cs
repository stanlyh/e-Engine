namespace Aprobaciones.Application.Abstracciones;

/// <summary>Consulta de datos de un usuario en Active Directory.</summary>
public interface IDirectorioUsuarios
{
    /// <param name="cuenta">DOMINIO\usuario</param>
    /// <returns>null si no se encuentra o el directorio no está disponible.</returns>
    DatosDirectorio? Buscar(string cuenta);
}

public sealed record DatosDirectorio(string Nombre, string? Email);
