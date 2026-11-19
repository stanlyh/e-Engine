namespace Aprobaciones.Domain.Notificaciones;

public enum EventoNotificacion : byte
{
    PorAprobar = 1,
    Rechazado = 2,
    Autorizado = 3,
    ParaTesoreria = 4,
    Procesado = 5,
    Prueba = 6,
}

public enum EstadoNotificacion : byte
{
    Pendiente = 1,
    Enviada = 2,
    /// <summary>Se agotaron los reintentos; Administración puede reintentarla.</summary>
    Fallida = 3,
    /// <summary>Nadie del destino tiene correo (o todos están excluidos).</summary>
    SinDestinatarios = 4,
}

/// <summary>
/// Correo en cola (patrón outbox): se guarda en la misma transacción que el cambio de estado del documento
/// y un proceso en segundo plano lo envía. Así una falla del servidor de correo no revierte una aprobación
/// y ningún aviso se pierde si el servidor se reinicia.
/// </summary>
public sealed class Notificacion
{
    /// <summary>Espera antes de cada reintento; al agotarse, la notificación queda Fallida.</summary>
    public static readonly TimeSpan[] Esperas =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)];

    private Notificacion() { }

    private Notificacion(EventoNotificacion evento, int? documentoId, string asunto, string cuerpoHtml, string cuerpoTexto, DateTime ahora)
    {
        if (string.IsNullOrWhiteSpace(asunto)) throw new DomainException("El asunto es obligatorio.");
        Evento = evento;
        DocumentoId = documentoId;
        Asunto = asunto.Length > 300 ? asunto[..300] : asunto;
        CuerpoHtml = cuerpoHtml;
        CuerpoTexto = cuerpoTexto;
        Estado = EstadoNotificacion.Pendiente;
        FechaCreacion = ahora;
        ProximoIntento = ahora;
    }

    public static Notificacion ParaUsuario(
        EventoNotificacion evento, int? documentoId, int usuarioId, string asunto, string cuerpoHtml, string cuerpoTexto, DateTime ahora) =>
        new(evento, documentoId, asunto, cuerpoHtml, cuerpoTexto, ahora) { UsuarioId = usuarioId };

    /// <param name="excluirCuentas">Cuentas DOMINIO\usuario del grupo que no deben recibirla (p. ej. quien cargó el documento).</param>
    public static Notificacion ParaGrupo(
        EventoNotificacion evento, int? documentoId, string grupoAd, IEnumerable<string> excluirCuentas,
        string asunto, string cuerpoHtml, string cuerpoTexto, DateTime ahora)
    {
        if (string.IsNullOrWhiteSpace(grupoAd)) throw new DomainException("El grupo de destino es obligatorio.");
        var excluir = string.Join(';', excluirCuentas.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase));
        return new(evento, documentoId, asunto, cuerpoHtml, cuerpoTexto, ahora)
        {
            GrupoAd = grupoAd,
            ExcluirCuentas = excluir.Length == 0 ? null : excluir,
        };
    }

    public long Id { get; private set; }
    public EventoNotificacion Evento { get; private set; }
    public int? DocumentoId { get; private set; }
    /// <summary>Destino: un usuario de la aplicación…</summary>
    public int? UsuarioId { get; private set; }
    /// <summary>…o los miembros de un grupo de AD, resueltos al enviar.</summary>
    public string? GrupoAd { get; private set; }
    /// <summary>Cuentas separadas por ';'.</summary>
    public string? ExcluirCuentas { get; private set; }
    public string Asunto { get; private set; } = "";
    public string CuerpoHtml { get; private set; } = "";
    public string CuerpoTexto { get; private set; } = "";
    public EstadoNotificacion Estado { get; private set; }
    public int Intentos { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public DateTime ProximoIntento { get; private set; }
    /// <summary>Evita que dos procesos envíen la misma notificación.</summary>
    public DateTime? BloqueadaHasta { get; private set; }
    public DateTime? FechaEnvio { get; private set; }
    /// <summary>Correos a los que se envió (o se intentó enviar), separados por ';'.</summary>
    public string? Destinatarios { get; private set; }
    public string? UltimoError { get; private set; }

    public IReadOnlyCollection<string> CuentasExcluidas =>
        ExcluirCuentas?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? [];

    public void MarcarEnviada(IReadOnlyCollection<string> destinatarios, DateTime ahora)
    {
        Estado = EstadoNotificacion.Enviada;
        Intentos++;
        Destinatarios = Recortar(string.Join(';', destinatarios), 2000);
        FechaEnvio = ahora;
        UltimoError = null;
        BloqueadaHasta = null;
    }

    public void MarcarSinDestinatarios(string detalle, DateTime ahora)
    {
        Estado = EstadoNotificacion.SinDestinatarios;
        UltimoError = Recortar(detalle, 1000);
        ProximoIntento = ahora;
        BloqueadaHasta = null;
    }

    /// <summary>Programa el siguiente intento con espera creciente; tras el último queda Fallida.</summary>
    public void RegistrarFallo(string error, IReadOnlyCollection<string> destinatarios, DateTime ahora)
    {
        Intentos++;
        UltimoError = Recortar(error, 1000);
        if (destinatarios.Count > 0) Destinatarios = Recortar(string.Join(';', destinatarios), 2000);
        BloqueadaHasta = null;
        if (Intentos > Esperas.Length)
        {
            Estado = EstadoNotificacion.Fallida;
            return;
        }
        ProximoIntento = ahora + Esperas[Intentos - 1];
    }

    /// <summary>Administración vuelve a poner en cola una notificación que no se pudo entregar.</summary>
    public void Reintentar(DateTime ahora)
    {
        if (Estado is not (EstadoNotificacion.Fallida or EstadoNotificacion.SinDestinatarios))
            throw new DomainException("Solo se reintentan notificaciones fallidas o sin destinatarios.");
        Estado = EstadoNotificacion.Pendiente;
        Intentos = 0;
        ProximoIntento = ahora;
        BloqueadaHasta = null;
    }

    private static string Recortar(string texto, int maximo) => texto.Length > maximo ? texto[..(maximo - 1)] + "…" : texto;
}
