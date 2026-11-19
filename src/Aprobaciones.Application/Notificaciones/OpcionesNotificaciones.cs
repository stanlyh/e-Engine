namespace Aprobaciones.Application.Notificaciones;

/// <summary>Sección "Notificaciones" de appsettings.</summary>
public sealed class OpcionesNotificaciones
{
    public const string Seccion = "Notificaciones";

    /// <summary>Dirección del sitio (p. ej. https://aprobaciones.empresa.local) para los enlaces de los correos. Vacía = sin enlace.</summary>
    public string? UrlBase { get; set; }

    public string NombreAplicacion { get; set; } = "Aprobación de documentos";

    /// <summary>Se toma de Seguridad:GrupoTesoreria; a él se avisa cuando un documento llega a Tesorería.</summary>
    public string? GrupoTesoreria { get; set; }
}
