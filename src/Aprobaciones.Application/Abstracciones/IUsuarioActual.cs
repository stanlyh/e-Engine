namespace Aprobaciones.Application.Abstracciones;

/// <summary>Usuario autenticado en la petición actual (Windows Authentication).</summary>
public interface IUsuarioActual
{
    int Id { get; }
    string Cuenta { get; }
    IReadOnlyCollection<string> Roles { get; }
    /// <summary>Grupos de aprobación (de las reglas) a los que pertenece.</summary>
    IReadOnlyCollection<string> Grupos { get; }
    bool EnRol(string rol);
    bool PerteneceAGrupo(string grupoAd);
}
