namespace Aprobaciones.Infrastructure.Correo;

public enum ModoCorreo
{
    /// <summary>Los avisos se quedan en cola sin enviarse.</summary>
    Desactivado,
    /// <summary>Escribe cada correo como archivo .eml en una carpeta (desarrollo y pruebas).</summary>
    Carpeta,
    Smtp,
}

/// <summary>Sección "Correo" de appsettings.</summary>
public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";

    public ModoCorreo Modo { get; init; } = ModoCorreo.Desactivado;

    /// <summary>Modo Carpeta: ruta absoluta donde se escriben los .eml.</summary>
    public string? Carpeta { get; init; }

    public string? Host { get; init; }
    public int Puerto { get; init; } = 25;
    public bool Ssl { get; init; }
    /// <summary>Autenticarse con la cuenta del grupo de aplicaciones de IIS (relay de Exchange en intranet).</summary>
    public bool UsarCredencialesWindows { get; init; }
    public string? Usuario { get; init; }
    public string? Contrasena { get; init; }

    public string Remitente { get; init; } = "";
    public string NombreRemitente { get; init; } = "Aprobación de documentos";

    /// <summary>Ambientes de prueba: todo se envía a esta dirección, con los destinatarios reales al inicio del cuerpo.</summary>
    public string? RedirigirA { get; init; }

    public int IntervaloSegundos { get; init; } = 15;
    public int Lote { get; init; } = 20;
}
