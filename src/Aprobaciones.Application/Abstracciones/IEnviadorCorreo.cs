namespace Aprobaciones.Application.Abstracciones;

/// <summary>Envío de correo (SMTP, carpeta de recogida en desarrollo, o desactivado).</summary>
public interface IEnviadorCorreo
{
    /// <summary>Smtp, Carpeta o Desactivado; se muestra en la pantalla de administración.</summary>
    string Modo { get; }

    /// <exception cref="Exception">Cualquier falla de entrega; quien llama decide si reintenta.</exception>
    Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct);
}

/// <param name="Para">Direcciones de correo de los destinatarios.</param>
public sealed record MensajeCorreo(IReadOnlyList<string> Para, string Asunto, string Html, string Texto);
