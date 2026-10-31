using System.Text;

namespace Aprobaciones.Infrastructure.Tests;

/// <summary>Genera XML CFDI 4.0 mínimos para pruebas.</summary>
internal static class CfdiDePrueba
{
    public const string Uuid = "6f1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d";

    public static byte[] Xml(
        string total = "1160.00",
        string moneda = "MXN",
        string? tipoCambio = null,
        string tipo = "I",
        string rfcReceptor = "EMP010101AAA",
        bool timbrado = true,
        string version = "4.0",
        string prologo = "")
    {
        var tc = tipoCambio is null ? "" : $" TipoCambio=\"{tipoCambio}\"";
        var timbre = timbrado
            ? $"""<cfdi:Complemento><tfd:TimbreFiscalDigital Version="1.1" UUID="{Uuid}" FechaTimbrado="2026-10-29T10:05:00" /></cfdi:Complemento>"""
            : "";
        return Encoding.UTF8.GetBytes($"""
            <?xml version="1.0" encoding="UTF-8"?>
            {prologo}
            <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital"
              Version="{version}" Serie="A" Folio="1001" Fecha="2026-10-29T10:00:00" SubTotal="1000.00" Total="{total}"
              Moneda="{moneda}"{tc} TipoDeComprobante="{tipo}" Exportacion="01" LugarExpedicion="64000">
              <cfdi:Emisor Rfc="aaa010101aaa" Nombre="PROVEEDOR DE PRUEBA" RegimenFiscal="601" />
              <cfdi:Receptor Rfc="{rfcReceptor}" Nombre="EMPRESA" DomicilioFiscalReceptor="64000" RegimenFiscalReceptor="601" UsoCFDI="G03" />
              <cfdi:Conceptos><cfdi:Concepto ClaveProdServ="84111506" Cantidad="1" ClaveUnidad="E48" Descripcion="Servicio" ValorUnitario="1000.00" Importe="1000.00" ObjetoImp="02" /></cfdi:Conceptos>
              {timbre}
            </cfdi:Comprobante>
            """.Trim());
    }
}
