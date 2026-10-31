using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Domain;
using Aprobaciones.Domain.Documentos;

namespace Aprobaciones.Infrastructure.Cfdi;

/// <summary>
/// Lee un CFDI 4.0 timbrado. El XML se procesa sin DTD ni resolución externa para evitar ataques XXE.
/// No valida el sello ni el estatus ante el SAT.
/// </summary>
internal sealed class LectorCfdi : ILectorCfdi
{
    private static readonly XNamespace Cfdi = "http://www.sat.gob.mx/cfd/4";
    private static readonly XNamespace Tfd = "http://www.sat.gob.mx/TimbreFiscalDigital";

    private static readonly XmlReaderSettings Configuracion = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersFromEntities = 0,
    };

    public DatosCfdi Leer(byte[] xml)
    {
        XDocument documento;
        try
        {
            using var lector = XmlReader.Create(new MemoryStream(xml), Configuracion);
            documento = XDocument.Load(lector);
        }
        catch (XmlException ex)
        {
            throw new DomainException($"El archivo no es un XML válido: {ex.Message}");
        }

        var comprobante = documento.Root;
        if (comprobante?.Name != Cfdi + "Comprobante")
            throw new DomainException("El XML no es un CFDI 4.0 (se esperaba cfdi:Comprobante con el namespace de la versión 4.0).");
        if (Atributo(comprobante, "Version") != "4.0")
            throw new DomainException("Solo se aceptan CFDI versión 4.0.");

        var emisor = Elemento(comprobante, Cfdi + "Emisor");
        var receptor = Elemento(comprobante, Cfdi + "Receptor");
        var timbre = comprobante.Element(Cfdi + "Complemento")?.Element(Tfd + "TimbreFiscalDigital")
            ?? throw new DomainException("El CFDI no está timbrado (falta tfd:TimbreFiscalDigital).");

        var moneda = Atributo(comprobante, "Moneda");
        var tipoCambio = OpcionalDecimal(comprobante, "TipoCambio");
        if (moneda is not ("MXN" or "XXX") && tipoCambio is null)
            throw new DomainException($"El CFDI está en {moneda} y no trae TipoCambio.");

        return new DatosCfdi(
            Uuid: Guid.TryParse(Atributo(timbre, "UUID"), out var uuid) ? uuid : throw new DomainException("El UUID del timbre no es válido."),
            RfcEmisor: Atributo(emisor, "Rfc").ToUpperInvariant(),
            NombreEmisor: emisor.Attribute("Nombre")?.Value ?? "",
            RfcReceptor: Atributo(receptor, "Rfc").ToUpperInvariant(),
            Serie: comprobante.Attribute("Serie")?.Value,
            Folio: comprobante.Attribute("Folio")?.Value,
            Fecha: DateTime.TryParseExact(Atributo(comprobante, "Fecha"), "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
                ? fecha
                : throw new DomainException("La fecha del CFDI no tiene el formato esperado."),
            SubTotal: Decimal(comprobante, "SubTotal"),
            Total: Decimal(comprobante, "Total"),
            Moneda: moneda,
            TipoCambio: tipoCambio,
            TipoComprobante: Atributo(comprobante, "TipoDeComprobante"));
    }

    private static XElement Elemento(XElement padre, XName nombre) =>
        padre.Element(nombre) ?? throw new DomainException($"Falta el nodo {nombre.LocalName} en el CFDI.");

    private static string Atributo(XElement elemento, string nombre) =>
        elemento.Attribute(nombre)?.Value is { Length: > 0 } valor
            ? valor.Trim()
            : throw new DomainException($"Falta el atributo {nombre} en {elemento.Name.LocalName}.");

    private static decimal Decimal(XElement elemento, string nombre) =>
        decimal.TryParse(Atributo(elemento, nombre), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : throw new DomainException($"El atributo {nombre} no es un importe válido.");

    private static decimal? OpcionalDecimal(XElement elemento, string nombre) =>
        elemento.Attribute(nombre) is null ? null : Decimal(elemento, nombre);
}
