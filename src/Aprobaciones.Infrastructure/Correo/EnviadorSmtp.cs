using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Aprobaciones.Application.Abstracciones;
using Microsoft.Extensions.Options;

namespace Aprobaciones.Infrastructure.Correo;

/// <summary>
/// Envío con System.Net.Mail: SMTP del corporativo, o carpeta de recogida (.eml) en desarrollo.
/// Todos los destinatarios van en "Para": son miembros del mismo grupo de aprobación, y la copia oculta
/// obligaría a poner al remitente como destinatario (copia de cada aviso a ese buzón y más riesgo de spam).
/// </summary>
internal sealed class EnviadorSmtp(IOptions<OpcionesCorreo> opciones) : IEnviadorCorreo
{
    private OpcionesCorreo Config => opciones.Value;

    public string Modo => Config.Modo.ToString();

    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
    {
        if (Config.Modo == ModoCorreo.Desactivado) throw new InvalidOperationException("El envío de correo está desactivado.");
        if (mensaje.Para.Count == 0) throw new ArgumentException("El mensaje no tiene destinatarios.", nameof(mensaje));

        var (para, html, texto) = Config.RedirigirA is { Length: > 0 } redirigir
            ? ([redirigir], AvisoRedireccionHtml(mensaje.Para) + mensaje.Html, AvisoRedireccionTexto(mensaje.Para) + mensaje.Texto)
            : (mensaje.Para, mensaje.Html, mensaje.Texto);

        using var correo = new MailMessage
        {
            From = new MailAddress(Config.Remitente, Config.NombreRemitente, Encoding.UTF8),
            Subject = mensaje.Asunto,
            SubjectEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            Body = texto,
            IsBodyHtml = false,
        };
        correo.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html));
        foreach (var direccion in para) correo.To.Add(direccion);

        using var cliente = Cliente();
        await cliente.SendMailAsync(correo, ct);
    }

    private SmtpClient Cliente()
    {
        if (Config.Modo == ModoCorreo.Carpeta)
        {
            Directory.CreateDirectory(Config.Carpeta!);
            return new SmtpClient { DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory, PickupDirectoryLocation = Config.Carpeta };
        }

        var cliente = new SmtpClient(Config.Host, Config.Puerto) { EnableSsl = Config.Ssl, DeliveryMethod = SmtpDeliveryMethod.Network };
        if (Config.UsarCredencialesWindows) cliente.UseDefaultCredentials = true;
        else if (!string.IsNullOrEmpty(Config.Usuario)) cliente.Credentials = new NetworkCredential(Config.Usuario, Config.Contrasena);
        return cliente;
    }

    private static string AvisoRedireccionHtml(IEnumerable<string> originales) =>
        $"<p style=\"background:#fef3c7;padding:8px 12px;font-family:Arial,sans-serif;font-size:13px\">Correo redirigido (ambiente de pruebas). Destinatarios reales: {WebUtility.HtmlEncode(string.Join(", ", originales))}</p>";

    private static string AvisoRedireccionTexto(IEnumerable<string> originales) =>
        $"[Correo redirigido (ambiente de pruebas). Destinatarios reales: {string.Join(", ", originales)}]{Environment.NewLine}{Environment.NewLine}";
}
