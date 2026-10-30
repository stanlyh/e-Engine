namespace Aprobaciones.Api.Seguridad;

/// <summary>Sección "Seguridad" de appsettings: grupos de AD que otorgan cada rol.</summary>
public sealed class OpcionesSeguridad
{
    public const string Seccion = "Seguridad";

    /// <summary>Grupo que puede cargar documentos. Vacío = cualquier usuario autenticado del dominio.</summary>
    public string? GrupoUsuarios { get; init; }
    public string GrupoTesoreria { get; init; } = "";
    public string GrupoAdmin { get; init; } = "";

    /// <summary>Solo en Development: grupos que se asignan al usuario actual sin consultar AD.</summary>
    public string[] GruposSimulados { get; init; } = [];
}

public static class TiposClaim
{
    public const string UsuarioId = "aprobaciones:uid";
    public const string Grupo = "aprobaciones:grupo";
}
