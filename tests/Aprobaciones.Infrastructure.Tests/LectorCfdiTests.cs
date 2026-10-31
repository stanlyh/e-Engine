using System.Text;
using Aprobaciones.Domain;
using Aprobaciones.Infrastructure.Cfdi;

namespace Aprobaciones.Infrastructure.Tests;

public class LectorCfdiTests
{
    private readonly LectorCfdi _lector = new();

    [Fact]
    public void Lee_los_datos_principales_del_cfdi()
    {
        var cfdi = _lector.Leer(CfdiDePrueba.Xml());

        Assert.Equal(Guid.Parse(CfdiDePrueba.Uuid), cfdi.Uuid);
        Assert.Equal("AAA010101AAA", cfdi.RfcEmisor);
        Assert.Equal("PROVEEDOR DE PRUEBA", cfdi.NombreEmisor);
        Assert.Equal("EMP010101AAA", cfdi.RfcReceptor);
        Assert.Equal("A", cfdi.Serie);
        Assert.Equal("1001", cfdi.Folio);
        Assert.Equal(new DateTime(2026, 10, 29, 10, 0, 0), cfdi.Fecha);
        Assert.Equal(1000.00m, cfdi.SubTotal);
        Assert.Equal(1160.00m, cfdi.Total);
        Assert.Equal("I", cfdi.TipoComprobante);
        Assert.Equal(1160.00m, cfdi.MontoMxn);
    }

    [Fact]
    public void Convierte_dolares_con_el_tipo_de_cambio()
    {
        var cfdi = _lector.Leer(CfdiDePrueba.Xml(total: "1000.00", moneda: "USD", tipoCambio: "18.50"));

        Assert.Equal(18_500m, cfdi.MontoMxn);
    }

    [Fact]
    public void Rechaza_moneda_extranjera_sin_tipo_de_cambio() =>
        Assert.Throws<DomainException>(() => _lector.Leer(CfdiDePrueba.Xml(moneda: "USD")));

    [Fact]
    public void Rechaza_cfdi_sin_timbre()
    {
        var ex = Assert.Throws<DomainException>(() => _lector.Leer(CfdiDePrueba.Xml(timbrado: false)));
        Assert.Contains("timbrado", ex.Message);
    }

    [Fact]
    public void Rechaza_otra_version() =>
        Assert.Throws<DomainException>(() => _lector.Leer(CfdiDePrueba.Xml(version: "3.3")));

    [Fact]
    public void Rechaza_xml_que_no_es_cfdi() =>
        Assert.Throws<DomainException>(() => _lector.Leer("<factura><total>10</total></factura>"u8.ToArray()));

    [Fact]
    public void Rechaza_archivo_que_no_es_xml() =>
        Assert.Throws<DomainException>(() => _lector.Leer("%PDF-1.7 no es xml"u8.ToArray()));

    [Fact]
    public void Rechaza_dtd_para_evitar_xxe()
    {
        var conDtd = CfdiDePrueba.Xml(prologo: """<!DOCTYPE foo [ <!ENTITY xxe SYSTEM "file:///c:/windows/win.ini"> ]>""");

        var ex = Assert.Throws<DomainException>(() => _lector.Leer(conDtd));
        Assert.Contains("DTD", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Acepta_xml_con_bom() =>
        Assert.Equal(1160.00m, _lector.Leer([.. Encoding.UTF8.GetPreamble(), .. CfdiDePrueba.Xml()]).Total);
}
