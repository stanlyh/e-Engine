using Aprobaciones.Domain;
using Aprobaciones.Domain.Documentos;

namespace Aprobaciones.Domain.Tests;

public class DocumentoTests
{
    private const int Creador = 1;
    private const int GerenteArea = 2;
    private const int GerenteFinanciero = 3;
    private const int Tesorero = 4;
    private static readonly DateTime Ahora = new(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyList<PasoRuta> RutaDosPasos =
    [
        new(1, "Gerente de Área", "APR_GerenteArea"),
        new(2, "Gerente Financiero", "APR_GerenteFinanciero"),
    ];

    private static Func<string, bool> Grupos(params string[] grupos) => g => grupos.Contains(g);

    private static DatosCfdi Cfdi(decimal total = 100_000m, string moneda = "MXN", decimal? tipoCambio = null) =>
        new(Guid.NewGuid(), "AAA010101AAA", "Proveedor SA", "EMP010101AAA", "A", "123", Ahora, total, total, moneda, tipoCambio, "I");

    private static ArchivoAlmacenado Archivo(string nombre) => new(nombre, $"2026/10/{nombre}", "application/octet-stream", 10, "hash");

    private static Documento DocumentoListo()
    {
        var doc = Documento.Crear(Creador, "Factura de servicios", Ahora);
        doc.RegistrarArchivo(Creador, TipoArchivo.Xml, Archivo("f.xml"), Cfdi(), Ahora);
        doc.RegistrarArchivo(Creador, TipoArchivo.Pdf, Archivo("f.pdf"), null, Ahora);
        return doc;
    }

    [Fact]
    public void Enviar_sin_ambos_archivos_falla()
    {
        var doc = Documento.Crear(Creador, "Factura", Ahora);
        doc.RegistrarArchivo(Creador, TipoArchivo.Xml, Archivo("f.xml"), Cfdi(), Ahora);

        var ex = Assert.Throws<DomainException>(() => doc.Enviar(Creador, RutaDosPasos, Ahora));
        Assert.Contains("XML y el PDF", ex.Message);
    }

    [Fact]
    public void Enviar_activa_el_primer_paso_y_deja_los_demas_en_espera()
    {
        var doc = DocumentoListo();

        doc.Enviar(Creador, RutaDosPasos, Ahora);

        Assert.Equal(EstadoDocumento.EnAprobacion, doc.Estado);
        Assert.Equal(1, doc.Ronda);
        Assert.Equal("Gerente de Área", doc.PasoActual?.Nombre);
        Assert.Equal([EstadoPaso.Pendiente, EstadoPaso.EnEspera], doc.RondaActual.Select(a => a.Estado));
    }

    [Fact]
    public void Aprobar_todos_los_pasos_lleva_a_tesoreria()
    {
        var doc = DocumentoListo();
        doc.Enviar(Creador, RutaDosPasos, Ahora);

        doc.Aprobar(GerenteArea, Grupos("APR_GerenteArea"), null, Ahora);
        Assert.Equal(EstadoDocumento.EnAprobacion, doc.Estado);
        Assert.Equal("Gerente Financiero", doc.PasoActual?.Nombre);

        doc.Aprobar(GerenteFinanciero, Grupos("APR_GerenteFinanciero"), "OK", Ahora);
        Assert.Equal(EstadoDocumento.EnTesoreria, doc.Estado);
        Assert.NotNull(doc.FechaAutorizacion);
        Assert.Null(doc.PasoActual);
    }

    [Fact]
    public void Aprobar_sin_pertenecer_al_grupo_del_paso_falla()
    {
        var doc = DocumentoListo();
        doc.Enviar(Creador, RutaDosPasos, Ahora);

        Assert.Throws<DomainException>(() => doc.Aprobar(GerenteFinanciero, Grupos("APR_GerenteFinanciero"), null, Ahora));
    }

    [Fact]
    public void El_creador_no_puede_aprobar_su_documento()
    {
        var doc = DocumentoListo();
        doc.Enviar(Creador, RutaDosPasos, Ahora);

        Assert.Throws<DomainException>(() => doc.Aprobar(Creador, Grupos("APR_GerenteArea"), null, Ahora));
    }

    [Fact]
    public void La_misma_persona_no_aprueba_dos_pasos()
    {
        var doc = DocumentoListo();
        doc.Enviar(Creador, RutaDosPasos, Ahora);
        var ambos = Grupos("APR_GerenteArea", "APR_GerenteFinanciero");

        doc.Aprobar(GerenteArea, ambos, null, Ahora);

        Assert.Throws<DomainException>(() => doc.Aprobar(GerenteArea, ambos, null, Ahora));
    }

    [Fact]
    public void Rechazar_exige_motivo_y_cancela_pasos_restantes()
    {
        var doc = DocumentoListo();
        doc.Enviar(Creador, RutaDosPasos, Ahora);

        Assert.Throws<DomainException>(() => doc.Rechazar(GerenteArea, Grupos("APR_GerenteArea"), "  ", Ahora));

        doc.Rechazar(GerenteArea, Grupos("APR_GerenteArea"), "El RFC no coincide", Ahora);

        Assert.Equal(EstadoDocumento.Rechazado, doc.Estado);
        Assert.Equal([EstadoPaso.Rechazado, EstadoPaso.Cancelado], doc.RondaActual.Select(a => a.Estado));
        Assert.Contains(doc.Historial, h => h.Accion == AccionHistorial.Rechazado && h.Comentario!.Contains("RFC"));
    }

    [Fact]
    public void Reenviar_tras_rechazo_inicia_nueva_ronda_desde_el_paso_uno()
    {
        var doc = DocumentoListo();
        doc.Enviar(Creador, RutaDosPasos, Ahora);
        doc.Aprobar(GerenteArea, Grupos("APR_GerenteArea"), null, Ahora);
        doc.Rechazar(GerenteFinanciero, Grupos("APR_GerenteFinanciero"), "Falta orden de compra", Ahora);

        doc.RegistrarArchivo(Creador, TipoArchivo.Pdf, Archivo("f2.pdf"), null, Ahora);
        doc.Enviar(Creador, RutaDosPasos, Ahora);

        Assert.Equal(2, doc.Ronda);
        Assert.Equal(EstadoDocumento.EnAprobacion, doc.Estado);
        Assert.Equal("Gerente de Área", doc.PasoActual?.Nombre);
        Assert.Equal(4, doc.Aprobaciones.Count);
        Assert.Contains(doc.Historial, h => h.Accion == AccionHistorial.Reenviado);
    }

    [Fact]
    public void Reemplazar_archivo_conserva_la_version_anterior()
    {
        var doc = DocumentoListo();

        doc.RegistrarArchivo(Creador, TipoArchivo.Pdf, Archivo("f2.pdf"), null, Ahora);

        var pdfs = doc.Archivos.Where(a => a.Tipo == TipoArchivo.Pdf).ToList();
        Assert.Equal(2, pdfs.Count);
        Assert.Equal(2, doc.ArchivoVigente(TipoArchivo.Pdf)!.Version);
        Assert.Single(pdfs, a => a.Vigente);
    }

    [Fact]
    public void No_se_edita_mientras_esta_en_aprobacion()
    {
        var doc = DocumentoListo();
        doc.Enviar(Creador, RutaDosPasos, Ahora);

        Assert.Throws<DomainException>(() => doc.RegistrarArchivo(Creador, TipoArchivo.Pdf, Archivo("x.pdf"), null, Ahora));
    }

    [Fact]
    public void Procesar_exige_estar_en_tesoreria_y_referencia()
    {
        var doc = DocumentoListo();
        doc.Enviar(Creador, [new(1, "Gerente de Área", "APR_GerenteArea")], Ahora);
        Assert.Throws<DomainException>(() => doc.Procesar(Tesorero, "TRF-1", null, Ahora));

        doc.Aprobar(GerenteArea, Grupos("APR_GerenteArea"), null, Ahora);
        Assert.Throws<DomainException>(() => doc.Procesar(Tesorero, "", null, Ahora));

        doc.Procesar(Tesorero, "TRF-1", "Pagado", Ahora);
        Assert.Equal(EstadoDocumento.Procesado, doc.Estado);
        Assert.Equal("TRF-1", doc.ReferenciaTesoreria);
    }

    [Fact]
    public void Monto_en_dolares_se_convierte_con_tipo_de_cambio()
    {
        Assert.Equal(185_000m, Cfdi(10_000m, "USD", 18.5m).MontoMxn);
        Assert.Throws<DomainException>(() => Cfdi(10_000m, "USD").MontoMxn);
    }
}
