using System.Net;
using Aprobaciones.Api.Tests.Infra;
using static Aprobaciones.Api.Tests.Infra.ClientePrueba;

namespace Aprobaciones.Api.Tests;

/// <summary>Permisos, antiforgery y acceso a documentos de otras personas.</summary>
public class SeguridadTests(AprobacionesFactory api) : IClassFixture<AprobacionesFactory>
{
    [Fact]
    public async Task Una_escritura_sin_token_XSRF_se_rechaza()
    {
        var sinToken = await api.ClienteAsync(Usuarios.Creador, conXsrf: false);

        var respuesta = await sinToken.PostAsync("/api/documentos", new { descripcion = "Sin token" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("antiforgery", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task El_token_XSRF_de_otra_persona_no_sirve()
    {
        var victima = await api.ClienteAsync(Usuarios.Creador);
        var atacante = await api.ClienteAsync(Usuarios.Ajeno);
        atacante.UsarToken(victima.TokenXsrf!);

        Assert.Equal(HttpStatusCode.BadRequest, (await atacante.PostAsync("/api/documentos", new { descripcion = "x" })).StatusCode);
    }

    [Fact]
    public async Task Un_documento_ajeno_responde_como_si_no_existiera()
    {
        var creador = await api.ClienteAsync(Usuarios.Creador);
        var ajeno = await api.ClienteAsync(Usuarios.Ajeno);
        var id = await creador.DocumentoListoAsync(3_000m, enviar: false);

        Assert.Equal(HttpStatusCode.NotFound, (await ajeno.GetAsync($"/api/documentos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ajeno.DeleteAsync($"/api/documentos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ajeno.PostAsync($"/api/documentos/{id}/enviar")).StatusCode);
        var lista = await ajeno.GetJsonAsync("/api/documentos");
        Assert.DoesNotContain(lista["elementos"]!.AsArray(), d => d!["id"]!.GetValue<int>() == id);
    }

    [Fact]
    public async Task Tesoreria_ve_el_documento_solo_cuando_ya_esta_autorizado()
    {
        var creador = await api.ClienteAsync(Usuarios.Creador);
        var gerente = await api.ClienteAsync(Usuarios.Gerente1);
        var tesorero = await api.ClienteAsync(Usuarios.Tesorero);
        var financiero = await api.ClienteAsync(Usuarios.Financiero);
        var id = await creador.DocumentoListoAsync(9_000m);

        Assert.Equal(HttpStatusCode.NotFound, (await tesorero.GetAsync($"/api/documentos/{id}")).StatusCode);
        // Un paso de un solo nivel: el Gerente Financiero no está en la cadena y no lo ve.
        Assert.Equal(HttpStatusCode.NotFound, (await financiero.GetAsync($"/api/documentos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await creador.PostAsync($"/api/documentos/{id}/procesar", new { referencia = "x" })).StatusCode);

        await gerente.AprobarAsync(id);

        Assert.Equal(HttpStatusCode.OK, (await tesorero.GetAsync($"/api/documentos/{id}")).StatusCode);
        Assert.True((await tesorero.DocumentoAsync(id))["permisos"]!["procesar"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Solo_Administracion_entra_a_las_pantallas_de_administracion()
    {
        var usuario = await api.ClienteAsync(Usuarios.Creador);
        var admin = await api.ClienteAsync(Usuarios.Admin);

        foreach (var url in new[] { "/api/admin/reglas", "/api/admin/correos", "/api/admin/correos/resumen", "/api/documentos?todos=true", "/api/dashboard/indicadores?todos=true" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await usuario.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await usuario.PostAsync("/api/admin/correos/prueba")).StatusCode);
    }

    [Fact]
    public async Task El_XML_siempre_se_descarga_y_el_PDF_se_muestra_en_linea()
    {
        var creador = await api.ClienteAsync(Usuarios.Creador);
        var id = await creador.DocumentoListoAsync(4_000m, enviar: false);
        var archivos = (await creador.DocumentoAsync(id))["archivos"]!.AsArray();
        int Archivo(string tipo) => archivos.Single(a => a!["tipo"]!.GetValue<string>() == tipo)!["id"]!.GetValue<int>();

        var xml = await creador.GetAsync($"/api/documentos/{id}/archivos/{Archivo("Xml")}");
        Assert.Equal("attachment", xml.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("sandbox", xml.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", xml.Headers.GetValues("X-Content-Type-Options").Single());

        var pdf = await creador.GetAsync($"/api/documentos/{id}/archivos/{Archivo("Pdf")}");
        Assert.Equal("inline", pdf.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(Cfdi.Pdf, await pdf.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Una_ruta_de_API_inexistente_responde_404()
    {
        var cliente = await api.ClienteAsync(Usuarios.Ajeno);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/api/no-existe")).StatusCode);
    }
}
