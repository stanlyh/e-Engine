using System.ComponentModel.DataAnnotations;

namespace Aprobaciones.Application.Documentos;

/// <summary>Sección "Documentos" de appsettings.</summary>
public sealed class OpcionesDocumentos
{
    public const string Seccion = "Documentos";

    /// <summary>Carpeta absoluta donde se guardan los XML y PDF ({año}/{mes}/{documentoId}/).</summary>
    [Required]
    public string RutaArchivos { get; init; } = "";

    [Range(1, 50)]
    public int TamanoMaximoMb { get; init; } = 10;

    /// <summary>RFC de la empresa; si se configura, el RFC receptor del CFDI debe coincidir.</summary>
    public string? RfcEmpresa { get; init; }

    /// <summary>Tipos de comprobante aceptados (I = ingreso).</summary>
    public string[] TiposComprobante { get; init; } = ["I"];

    public long TamanoMaximoBytes => TamanoMaximoMb * 1024L * 1024L;
}
