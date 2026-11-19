using System.Globalization;
using System.Net;
using System.Text;
using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Domain.Documentos;
using Aprobaciones.Domain.Notificaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aprobaciones.Application.Notificaciones;

/// <summary>
/// Deja en la cola los correos de cada transición. Se llama antes de SaveChanges, así que el aviso
/// se guarda en la misma transacción que el cambio de estado.
/// </summary>
public sealed class Notificador(
    IAprobacionesDbContext db,
    IUsuarioActual usuario,
    IOptions<OpcionesNotificaciones> opciones,
    TimeProvider reloj)
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;
    private OpcionesNotificaciones Config => opciones.Value;

    /// <summary>Tras enviar, reenviar o aprobar un paso intermedio: avisa al grupo del paso que sigue.</summary>
    public async Task AvisarPasoPendienteAsync(Documento documento, CancellationToken ct)
    {
        if (documento.PasoActual is not { } paso) return;

        // Ni quien cargó el documento ni quien ya aprobó un paso de esta ronda pueden resolver el siguiente.
        var excluirIds = documento.RondaActual.Where(a => a.ResueltoPorId is not null).Select(a => a.ResueltoPorId!.Value)
            .Append(documento.CreadoPorId).Distinct().ToList();
        var excluir = await db.Usuarios.AsNoTracking().Where(u => excluirIds.Contains(u.Id)).Select(u => u.Cuenta).ToListAsync(ct);
        var datos = await DatosAsync(documento, ct);

        var correo = new Correo($"Por aprobar: {documento.Folio} · {Monto(documento.MontoMxn)} · {datos.Emisor}")
            .Titulo($"Tienes un documento por aprobar ({paso.Nombre})")
            .Parrafo($"{datos.Creador} envió un documento que espera tu decisión.")
            .Detalles(Detalles(documento, datos).Append(("Tu paso", $"{paso.Nombre} (paso {paso.Orden} de {documento.RondaActual.Count()})")))
            .Enlace(Url(documento.Id, "aprobaciones"), "Revisar y resolver");
        db.Notificaciones.Add(correo.ParaGrupo(EventoNotificacion.PorAprobar, documento.Id, paso.GrupoAd, excluir, Ahora));
    }

    /// <summary>Último paso aprobado: avisa a quien lo cargó y a Tesorería.</summary>
    public async Task AvisarAutorizadoAsync(Documento documento, string? comentario, CancellationToken ct)
    {
        var datos = await DatosAsync(documento, ct);

        var alCreador = new Correo($"Autorizado: {documento.Folio} pasó a Tesorería")
            .Titulo("Tu documento fue autorizado")
            .Parrafo($"{datos.Actor} aprobó el último paso. El documento pasó a Tesorería para su pago.")
            .Detalles(Detalles(documento, datos).Concat(Comentario("Comentario", comentario)))
            .Enlace(Url(documento.Id, null), "Ver el documento");
        db.Notificaciones.Add(alCreador.ParaUsuario(EventoNotificacion.Autorizado, documento.Id, documento.CreadoPorId, Ahora));

        if (string.IsNullOrWhiteSpace(Config.GrupoTesoreria)) return;
        var aTesoreria = new Correo($"Por procesar en Tesorería: {documento.Folio} · {Monto(documento.MontoMxn)} · {datos.Emisor}")
            .Titulo("Hay un documento autorizado por procesar")
            .Parrafo("El documento completó su cadena de aprobación y espera el pago.")
            .Detalles(Detalles(documento, datos))
            .Enlace(Url(documento.Id, "tesoreria"), "Procesar en Tesorería");
        db.Notificaciones.Add(aTesoreria.ParaGrupo(EventoNotificacion.ParaTesoreria, documento.Id, Config.GrupoTesoreria, [], Ahora));
    }

    public async Task AvisarRechazoAsync(Documento documento, string motivo, CancellationToken ct)
    {
        var datos = await DatosAsync(documento, ct);
        var paso = documento.RondaActual.LastOrDefault(a => a.Estado == EstadoPaso.Rechazado);

        var correo = new Correo($"Rechazado: {documento.Folio}, corrígelo y reenvíalo")
            .Titulo("Tu documento fue rechazado")
            .Parrafo($"{datos.Actor} lo rechazó{(paso is null ? "" : $" en el paso {paso.Nombre}")}. Corrige lo necesario y reenvíalo; la aprobación empezará desde el primer paso.")
            .Detalles(Detalles(documento, datos).Concat(Comentario("Motivo", motivo)))
            .Enlace(Url(documento.Id, null), "Corregir el documento");
        db.Notificaciones.Add(correo.ParaUsuario(EventoNotificacion.Rechazado, documento.Id, documento.CreadoPorId, Ahora));
    }

    public async Task AvisarProcesadoAsync(Documento documento, string? comentario, CancellationToken ct)
    {
        var datos = await DatosAsync(documento, ct);

        var correo = new Correo($"Procesado: {documento.Folio} (Ref. {documento.ReferenciaTesoreria})")
            .Titulo("Tesorería procesó tu documento")
            .Parrafo($"{datos.Actor} lo marcó como procesado.")
            .Detalles(Detalles(documento, datos).Append(("Referencia", documento.ReferenciaTesoreria ?? "")).Concat(Comentario("Comentario", comentario)))
            .Enlace(Url(documento.Id, null), "Ver el documento");
        db.Notificaciones.Add(correo.ParaUsuario(EventoNotificacion.Procesado, documento.Id, documento.CreadoPorId, Ahora));
    }

    /// <summary>Correo de prueba al usuario actual, por el mismo camino que los demás.</summary>
    public Notificacion CrearPrueba(string nombreUsuario)
    {
        var correo = new Correo($"Correo de prueba de {Config.NombreAplicacion}")
            .Titulo("El envío de correos funciona")
            .Parrafo($"{nombreUsuario} solicitó este correo desde Administración. Si lo recibes, los avisos de aprobación llegarán igual.")
            .Enlace(string.IsNullOrWhiteSpace(Config.UrlBase) ? null : Config.UrlBase.TrimEnd('/') + "/", $"Abrir {Config.NombreAplicacion}");
        var notificacion = correo.ParaUsuario(EventoNotificacion.Prueba, null, usuario.Id, Ahora);
        db.Notificaciones.Add(notificacion);
        return notificacion;
    }

    private sealed record DatosCorreo(string Creador, string Actor, string Emisor);

    private async Task<DatosCorreo> DatosAsync(Documento documento, CancellationToken ct)
    {
        var ids = new[] { documento.CreadoPorId, usuario.Id };
        var nombres = await db.Usuarios.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);
        return new DatosCorreo(
            nombres.GetValueOrDefault(documento.CreadoPorId, "Alguien"),
            nombres.GetValueOrDefault(usuario.Id, "Alguien"),
            documento.Cfdi?.NombreEmisor is { Length: > 0 } e ? e : documento.Cfdi?.RfcEmisor ?? "");
    }

    private static IEnumerable<(string, string)> Detalles(Documento d, DatosCorreo datos)
    {
        yield return ("Folio", d.Folio);
        yield return ("Descripción", d.Descripcion);
        if (d.Cfdi is { } c)
        {
            yield return ("Emisor", $"{datos.Emisor} ({c.RfcEmisor})");
            yield return ("Monto", c.Moneda is "MXN" or "XXX"
                ? Monto(d.MontoMxn)
                : $"{Monto(d.MontoMxn)} ({c.Total.ToString("N2", Mx)} {c.Moneda})");
        }
        yield return ("Cargó", datos.Creador);
    }

    private static IEnumerable<(string, string)> Comentario(string etiqueta, string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? [] : [(etiqueta, texto.Trim())];

    private static string Monto(decimal? monto) => monto is { } m ? m.ToString("C2", Mx) : "—";

    private string? Url(int documentoId, string? desde) =>
        string.IsNullOrWhiteSpace(Config.UrlBase)
            ? null
            : $"{Config.UrlBase.TrimEnd('/')}/documentos/{documentoId}{(desde is null ? "" : $"?desde={desde}")}";

    /// <summary>Arma el mismo contenido en HTML (con todo el texto codificado) y en texto plano.</summary>
    private sealed class Correo(string asunto)
    {
        private readonly StringBuilder _html = new();
        private readonly StringBuilder _texto = new();

        public Correo Titulo(string texto)
        {
            _html.Append($"<h1 style=\"font-size:18px;margin:0 0 12px;color:#0f172a\">{Cod(texto)}</h1>");
            _texto.AppendLine(texto).AppendLine();
            return this;
        }

        public Correo Parrafo(string texto)
        {
            _html.Append($"<p style=\"margin:0 0 16px;color:#334155\">{Cod(texto)}</p>");
            _texto.AppendLine(texto).AppendLine();
            return this;
        }

        public Correo Detalles(IEnumerable<(string Etiqueta, string Valor)> filas)
        {
            _html.Append("<table style=\"border-collapse:collapse;margin:0 0 16px;font-size:14px\">");
            foreach (var (etiqueta, valor) in filas)
            {
                _html.Append($"<tr><td style=\"padding:4px 16px 4px 0;color:#64748b;vertical-align:top;white-space:nowrap\">{Cod(etiqueta)}</td>")
                     .Append($"<td style=\"padding:4px 0;color:#0f172a\">{Cod(valor)}</td></tr>");
                _texto.AppendLine($"{etiqueta}: {valor}");
            }
            _html.Append("</table>");
            _texto.AppendLine();
            return this;
        }

        public Correo Enlace(string? url, string texto)
        {
            if (url is null) return this;
            _html.Append($"<p style=\"margin:0 0 16px\"><a href=\"{Cod(url)}\" style=\"display:inline-block;background:#4f46e5;color:#ffffff;padding:10px 16px;border-radius:6px;text-decoration:none\">{Cod(texto)}</a></p>");
            _texto.AppendLine($"{texto}: {url}");
            return this;
        }

        public Notificacion ParaUsuario(EventoNotificacion evento, int? documentoId, int usuarioId, DateTime ahora) =>
            Notificacion.ParaUsuario(evento, documentoId, usuarioId, asunto, Html(), _texto.ToString(), ahora);

        public Notificacion ParaGrupo(EventoNotificacion evento, int? documentoId, string grupo, IEnumerable<string> excluir, DateTime ahora) =>
            Notificacion.ParaGrupo(evento, documentoId, grupo, excluir, asunto, Html(), _texto.ToString(), ahora);

        private string Html() =>
            "<!doctype html><html lang=\"es\"><body style=\"font-family:Segoe UI,Arial,sans-serif;background:#f8fafc;padding:24px\">"
            + $"<div style=\"max-width:560px;margin:auto;background:#ffffff;border:1px solid #e2e8f0;border-radius:8px;padding:24px\">{_html}"
            + "<p style=\"margin:16px 0 0;font-size:12px;color:#94a3b8\">Aviso automático; no respondas a este correo.</p></div></body></html>";

        private static string Cod(string texto) => WebUtility.HtmlEncode(texto);
    }
}
