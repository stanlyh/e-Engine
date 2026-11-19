using Aprobaciones.Domain.Documentos;

namespace Aprobaciones.Domain.Tests;

public class DocumentoEliminacionTests
{
    private const int Creador = 1;
    private const int Otro = 2;
    private static readonly DateTime Ahora = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    private static DatosCfdi Cfdi() =>
        new(Guid.NewGuid(), "AAA010101AAA", "Proveedor SA", "EMP010101AAA", "A", "1", Ahora, 1_000m, 1_000m, "MXN", null, "I");

    private static ArchivoAlmacenado Archivo(string ruta) => new(Path.GetFileName(ruta), ruta, "application/octet-stream", 10, "hash");

    private static Documento ConArchivos()
    {
        var doc = Documento.Crear(Creador, "Factura", Ahora);
        doc.RegistrarArchivo(Creador, TipoArchivo.Xml, Archivo("2026/11/1/a.xml"), Cfdi(), Ahora);
        doc.RegistrarArchivo(Creador, TipoArchivo.Xml, Archivo("2026/11/1/b.xml"), Cfdi(), Ahora); // versión anterior incluida
        doc.RegistrarArchivo(Creador, TipoArchivo.Pdf, Archivo("2026/11/1/c.pdf"), null, Ahora);
        return doc;
    }

    [Fact]
    public void Borrador_vacio_se_puede_eliminar_y_no_hay_archivos_que_borrar()
    {
        var doc = Documento.Crear(Creador, "Factura", Ahora);

        Assert.True(doc.EsEliminablePor(Creador));
        Assert.Empty(doc.PrepararEliminacion(Creador));
    }

    [Fact]
    public void Eliminar_devuelve_todas_las_versiones_de_los_archivos()
    {
        var doc = ConArchivos();

        Assert.Equal(["2026/11/1/a.xml", "2026/11/1/b.xml", "2026/11/1/c.pdf"], doc.PrepararEliminacion(Creador).Order());
    }

    [Fact]
    public void Otra_persona_no_puede_eliminar()
    {
        var doc = ConArchivos();

        Assert.False(doc.EsEliminablePor(Otro));
        var ex = Assert.Throws<DomainException>(() => doc.PrepararEliminacion(Otro));
        Assert.Contains("Solo quien creó", ex.Message);
    }

    [Fact]
    public void Un_documento_enviado_no_se_puede_eliminar()
    {
        var doc = ConArchivos();
        doc.Enviar(Creador, [new PasoRuta(1, "Gerente", "APR_Gerente")], Ahora);

        Assert.False(doc.EsEliminablePor(Creador));
        Assert.Throws<DomainException>(() => doc.PrepararEliminacion(Creador));
    }

    [Fact]
    public void Un_rechazado_tampoco_se_puede_eliminar_aunque_sea_editable()
    {
        var doc = ConArchivos();
        doc.Enviar(Creador, [new PasoRuta(1, "Gerente", "APR_Gerente")], Ahora);
        doc.Rechazar(Otro, _ => true, "Falta orden de compra", Ahora);

        Assert.True(doc.EsEditable);
        Assert.False(doc.EsEliminablePor(Creador));
        Assert.Throws<DomainException>(() => doc.PrepararEliminacion(Creador));
    }
}
