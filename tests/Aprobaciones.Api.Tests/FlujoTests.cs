using System.Net;
using Aprobaciones.Api.Tests.Infra;
using Aprobaciones.Domain.Documentos;
using Microsoft.EntityFrameworkCore;
using static Aprobaciones.Api.Tests.Infra.ClientePrueba;

namespace Aprobaciones.Api.Tests;

/// <summary>El ciclo de vida de un documento de punta a punta, por la API real y contra SQL Server.</summary>
public class FlujoTests(AprobacionesFactory api) : IClassFixture<AprobacionesFactory>
{
    private static string[] Acciones(System.Text.Json.Nodes.JsonNode doc) =>
        [.. doc["historial"]!.AsArray().Select(h => h!["accion"]!.GetValue<string>())];

    [Fact]
    public async Task Dos_pasos_de_aprobacion_y_Tesoreria_con_sus_avisos()
    {
        var creador = await api.ClienteAsync(Usuarios.Creador);
        var gerente = await api.ClienteAsync(Usuarios.Gerente1);
        var otroGerente = await api.ClienteAsync(Usuarios.Gerente2);
        var financiero = await api.ClienteAsync(Usuarios.Financiero);
        var tesorero = await api.ClienteAsync(Usuarios.Tesorero);

        // $120,000 cae en el rango de dos pasos: Gerente de Área → Gerente Financiero
        var id = await creador.DocumentoListoAsync(120_000m);
        var doc = await creador.DocumentoAsync(id);
        Assert.Equal("EnAprobacion", doc["estado"]!.GetValue<string>());
        var folio = doc["folio"]!.GetValue<string>();

        await gerente.AprobarAsync(id, "Visto bueno");
        // El paso 2 es de otro grupo: un Gerente de Área no puede resolverlo.
        var ajeno = await otroGerente.PostAsync($"/api/documentos/{id}/aprobar", new { comentario = "" });
        Assert.Equal(HttpStatusCode.BadRequest, ajeno.StatusCode);
        Assert.Contains("No perteneces al grupo", await DetalleAsync(ajeno));

        await financiero.AprobarAsync(id);
        Assert.Equal("EnTesoreria", (await creador.DocumentoAsync(id))["estado"]!.GetValue<string>());

        await Esperar(await tesorero.PostAsync($"/api/documentos/{id}/procesar", new { referencia = "SPEI-123" }), HttpStatusCode.NoContent);
        doc = await creador.DocumentoAsync(id);
        Assert.Equal("Procesado", doc["estado"]!.GetValue<string>());
        Assert.Equal("SPEI-123", doc["referenciaTesoreria"]!.GetValue<string>());
        Assert.Equal(["Creado", "ArchivoCargado", "ArchivoCargado", "Enviado", "PasoAprobado", "Autorizado", "Procesado"], Acciones(doc));

        await api.ProcesarCorreosAsync();
        Assert.Equal(
        [
            ($"Por aprobar: {folio} · $120,000.00 · PROVEEDOR & ASOCIADOS", "gabriela@empresa.test;gerardo@empresa.test"),
            ($"Por aprobar: {folio} · $120,000.00 · PROVEEDOR & ASOCIADOS", "fernanda@empresa.test"),
            ($"Autorizado: {folio} pasó a Tesorería", "carla@empresa.test"),
            ($"Por procesar en Tesorería: {folio} · $120,000.00 · PROVEEDOR & ASOCIADOS", "tomas@empresa.test"),
            ($"Procesado: {folio} (Ref. SPEI-123)", "carla@empresa.test"),
        ], api.Correo.De(folio));
    }

    [Fact]
    public async Task Un_rechazo_regresa_al_creador_y_el_reenvio_empieza_desde_el_primer_paso()
    {
        var creador = await api.ClienteAsync(Usuarios.Creador);
        var gerente = await api.ClienteAsync(Usuarios.Gerente1);
        var financiero = await api.ClienteAsync(Usuarios.Financiero);

        var id = await creador.DocumentoListoAsync(80_000m);
        await gerente.AprobarAsync(id);
        await Esperar(await financiero.PostAsync($"/api/documentos/{id}/rechazar", new { motivo = "Falta la orden de compra" }), HttpStatusCode.NoContent);

        var doc = await creador.DocumentoAsync(id);
        Assert.Equal("Rechazado", doc["estado"]!.GetValue<string>());
        Assert.True(doc["permisos"]!["editar"]!.GetValue<bool>());
        Assert.False(doc["permisos"]!["eliminar"]!.GetValue<bool>());

        // Corrige el PDF y reenvía: ronda 2 desde el paso 1; quien aprobó en la ronda anterior puede volver a aprobar.
        await Esperar(await creador.SubirAsync(id, "pdf", Cfdi.Pdf, "factura-corregida.pdf"), HttpStatusCode.OK);
        await Esperar(await creador.PostAsync($"/api/documentos/{id}/enviar"), HttpStatusCode.NoContent);
        doc = await creador.DocumentoAsync(id);
        Assert.Equal(2, doc["ronda"]!.GetValue<int>());
        var ronda2 = doc["aprobaciones"]!.AsArray().Where(a => a!["ronda"]!.GetValue<int>() == 2).ToList();
        Assert.Equal(["Pendiente", "EnEspera"], ronda2.Select(a => a!["estado"]!.GetValue<string>()));
        Assert.Equal("Reenviado", Acciones(doc)[^1]);
        await gerente.AprobarAsync(id);
    }

    [Fact]
    public async Task Nadie_aprueba_su_propio_documento_aunque_sea_del_grupo()
    {
        var gerente = await api.ClienteAsync(Usuarios.Gerente1);
        var id = await gerente.DocumentoListoAsync(5_000m);

        var respuesta = await gerente.PostAsync($"/api/documentos/{id}/aprobar", new { comentario = "" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("No puedes aprobar ni rechazar tu propio documento.", await DetalleAsync(respuesta));
        Assert.False((await gerente.DocumentoAsync(id))["permisos"]!["resolver"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Dos_aprobaciones_simultaneas_del_mismo_paso_solo_registran_una()
    {
        var creador = await api.ClienteAsync(Usuarios.Creador);
        var uno = await api.ClienteAsync(Usuarios.Gerente1);
        var dos = await api.ClienteAsync(Usuarios.Gerente2);
        var id = await creador.DocumentoListoAsync(7_000m);

        var respuestas = await Task.WhenAll(
            uno.PostAsync($"/api/documentos/{id}/aprobar", new { comentario = "uno" }),
            dos.PostAsync($"/api/documentos/{id}/aprobar", new { comentario = "dos" }));

        var codigos = respuestas.Select(r => r.StatusCode).Order().ToList();
        Assert.Equal(HttpStatusCode.NoContent, codigos[0]);
        // La otra choca con la versión de la fila (409) o, si llegó después, con que ya no está en aprobación (400).
        Assert.Contains(codigos[1], new[] { HttpStatusCode.Conflict, HttpStatusCode.BadRequest });
        var (aprobados, autorizados) = await api.ConBaseAsync(async db => (
            await db.DocumentoAprobaciones.CountAsync(a => a.DocumentoId == id && a.Estado == EstadoPaso.Aprobado),
            await db.DocumentoHistorial.CountAsync(h => h.DocumentoId == id && h.Accion == AccionHistorial.Autorizado)));
        Assert.Equal((1, 1), (aprobados, autorizados));
    }

    [Fact]
    public async Task Una_factura_no_se_registra_dos_veces_ni_con_otro_receptor()
    {
        var creador = await api.ClienteAsync(Usuarios.Creador);
        var uuid = Guid.NewGuid();
        await creador.DocumentoListoAsync(1_000m, enviar: false, uuid: uuid);

        var otro = await creador.CrearAsync("Intento duplicado");
        var duplicado = await creador.SubirAsync(otro, "xml", Cfdi.Xml(1_000m, uuid: uuid), "copia.xml");
        Assert.Equal(HttpStatusCode.BadRequest, duplicado.StatusCode);
        Assert.Contains("ya está registrada", await DetalleAsync(duplicado));

        var ajena = await creador.SubirAsync(otro, "xml", Cfdi.Xml(1_000m, receptor: "XAXX010101000"), "ajena.xml");
        Assert.Equal(HttpStatusCode.BadRequest, ajena.StatusCode);
        Assert.Contains("no al de la empresa", await DetalleAsync(ajena));

        var pdfFalso = await creador.SubirAsync(otro, "pdf", "no soy un pdf"u8.ToArray(), "falso.pdf");
        Assert.Equal(HttpStatusCode.BadRequest, pdfFalso.StatusCode);
    }

    [Fact]
    public async Task Eliminar_un_borrador_borra_sus_registros_y_sus_archivos()
    {
        var creador = await api.ClienteAsync(Usuarios.Creador);
        var id = await creador.DocumentoListoAsync(2_000m, enviar: false);
        var carpetas = Directory.GetDirectories(api.RutaArchivos, id.ToString(), SearchOption.AllDirectories);
        Assert.Single(carpetas);
        Assert.Equal(2, Directory.GetFiles(carpetas[0]).Length);

        await Esperar(await creador.DeleteAsync($"/api/documentos/{id}"), HttpStatusCode.NoContent);

        Assert.False(Directory.Exists(carpetas[0]));
        Assert.Equal(HttpStatusCode.NotFound, (await creador.GetAsync($"/api/documentos/{id}")).StatusCode);
        Assert.Equal(0, await api.ConBaseAsync(db => db.DocumentoHistorial.CountAsync(h => h.DocumentoId == id)));
    }

    [Fact]
    public async Task Los_indicadores_cuentan_los_documentos_enviados_de_cada_usuario()
    {
        var usuario = new Usuario(@"PRUEBA\indicadores", "Inés Indicadores", "ines@empresa.test");
        api.Directorio.Registrar(usuario);
        var cliente = await api.ClienteAsync(usuario);
        var gerente = await api.ClienteAsync(Usuarios.Gerente1);

        await cliente.DocumentoListoAsync(1_500m, enviar: false); // borrador: no cuenta
        await cliente.DocumentoListoAsync(2_500m);
        var autorizado = await cliente.DocumentoListoAsync(10_000m);
        await gerente.AprobarAsync(autorizado);

        var indicadores = (await cliente.GetJsonAsync("/api/dashboard/indicadores"))["indicadores"]!;
        decimal Monto(string clave) => indicadores[clave]!["monto"]!.GetValue<decimal>();
        int Cantidad(string clave) => indicadores[clave]!["cantidad"]!.GetValue<int>();
        Assert.Equal((2, 12_500m), (Cantidad("enviados"), Monto("enviados")));
        Assert.Equal((1, 2_500m), (Cantidad("pendientes"), Monto("pendientes")));
        Assert.Equal((1, 10_000m), (Cantidad("enTesoreria"), Monto("enTesoreria")));
    }
}
