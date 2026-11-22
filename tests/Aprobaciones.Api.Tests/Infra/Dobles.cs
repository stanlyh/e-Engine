using System.Collections.Concurrent;
using Aprobaciones.Application.Abstracciones;

namespace Aprobaciones.Api.Tests.Infra;

/// <summary>Usuarios de prueba con sus grupos de AD.</summary>
public sealed record Usuario(string Cuenta, string Nombre, string? Email, params string[] Grupos);

public static class Usuarios
{
    public static readonly Usuario Creador = new(@"PRUEBA\creador", "Carla Creadora", "carla@empresa.test");
    public static readonly Usuario Gerente1 = new(@"PRUEBA\gerente1", "Gerardo Uno", "gerardo@empresa.test", "APR_GerenteArea");
    public static readonly Usuario Gerente2 = new(@"PRUEBA\gerente2", "Gabriela Dos", "gabriela@empresa.test", "APR_GerenteArea");
    public static readonly Usuario Financiero = new(@"PRUEBA\financiero", "Fernanda Finanzas", "fernanda@empresa.test", "APR_GerenteFinanciero");
    public static readonly Usuario Director = new(@"PRUEBA\director", "Director sin correo", null, "APR_DirectorGeneral");
    public static readonly Usuario Tesorero = new(@"PRUEBA\tesorero", "Tomás Tesorero", "tomas@empresa.test", "APR_Tesoreria");
    public static readonly Usuario Admin = new(@"PRUEBA\admin", "Adriana Admin", "adriana@empresa.test", "APR_Admin");
    public static readonly Usuario Ajeno = new(@"PRUEBA\ajeno", "Andrés Ajeno", "andres@empresa.test");

    public static readonly Usuario[] Todos = [Creador, Gerente1, Gerente2, Financiero, Director, Tesorero, Admin, Ajeno];
}

/// <summary>Active Directory falso: usuarios y grupos fijos; se puede simular que el directorio no responde.</summary>
public sealed class DirectorioPrueba : IDirectorioUsuarios
{
    private readonly ConcurrentDictionary<string, Usuario> _usuarios = new(StringComparer.OrdinalIgnoreCase);

    public DirectorioPrueba()
    {
        foreach (var u in Usuarios.Todos) Registrar(u);
    }

    public volatile bool Disponible = true;

    public void Registrar(Usuario usuario) => _usuarios[usuario.Cuenta] = usuario;

    public DatosDirectorio? Buscar(string cuenta) =>
        Disponible && _usuarios.TryGetValue(cuenta, out var u) ? new DatosDirectorio(u.Nombre, u.Email) : null;

    public ResultadoGrupo MiembrosDeGrupo(string grupoAd)
    {
        if (!Disponible) return ResultadoGrupo.NoDisponible;
        var miembros = _usuarios.Values.Where(u => u.Grupos.Contains(grupoAd, StringComparer.OrdinalIgnoreCase))
            .Select(u => new MiembroDirectorio(u.Cuenta, u.Nombre, u.Email)).ToList();
        return miembros.Count == 0 ? ResultadoGrupo.Inexistente : new ResultadoGrupo(true, true, miembros);
    }
}

/// <summary>Captura los correos en lugar de enviarlos; puede simular un servidor SMTP caído.</summary>
public sealed class EnviadorPrueba : IEnviadorCorreo
{
    private readonly ConcurrentQueue<MensajeCorreo> _enviados = new();

    public string Modo => "Prueba";

    /// <summary>Si tiene valor, cada envío falla con este mensaje.</summary>
    public volatile string? FallarCon;

    public IReadOnlyList<MensajeCorreo> Enviados => [.. _enviados];

    public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
    {
        if (FallarCon is { } error) throw new System.Net.Mail.SmtpException(error);
        _enviados.Enqueue(mensaje);
        return Task.CompletedTask;
    }

    /// <summary>Destinatarios ordenados de los correos cuyo asunto contiene el texto (p. ej. un folio).</summary>
    public List<(string Asunto, string Para)> De(string texto) =>
        [.. Enviados.Where(m => m.Asunto.Contains(texto)).Select(m => (m.Asunto, string.Join(";", m.Para.Order(StringComparer.OrdinalIgnoreCase))))];
}
