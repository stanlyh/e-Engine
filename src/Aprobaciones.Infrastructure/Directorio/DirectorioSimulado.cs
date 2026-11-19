using Aprobaciones.Application.Abstracciones;
using Microsoft.Extensions.Options;

namespace Aprobaciones.Infrastructure.Directorio;

/// <summary>
/// Solo en desarrollo (sección DirectorioSimulado de appsettings): usuarios, correos y grupos fijos,
/// para probar los avisos por correo en un equipo fuera de dominio.
/// </summary>
internal sealed class DirectorioSimulado(IOptions<OpcionesDirectorioSimulado> opciones) : IDirectorioUsuarios
{
    public DatosDirectorio? Buscar(string cuenta) =>
        opciones.Value.Usuarios.FirstOrDefault(u => string.Equals(u.Cuenta, cuenta, StringComparison.OrdinalIgnoreCase))
            is { } u ? new DatosDirectorio(u.Nombre, u.Email) : null;

    public ResultadoGrupo MiembrosDeGrupo(string grupoAd)
    {
        var miembros = opciones.Value.Usuarios
            .Where(u => u.Grupos.Contains(grupoAd, StringComparer.OrdinalIgnoreCase))
            .Select(u => new MiembroDirectorio(u.Cuenta, u.Nombre, u.Email))
            .ToList();
        return miembros.Count == 0 ? ResultadoGrupo.Inexistente : new ResultadoGrupo(true, true, miembros);
    }
}

public sealed class OpcionesDirectorioSimulado
{
    public const string Seccion = "DirectorioSimulado";

    public List<UsuarioSimulado> Usuarios { get; init; } = [];
}

public sealed class UsuarioSimulado
{
    public string Cuenta { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string? Email { get; init; }
    public string[] Grupos { get; init; } = [];
}
