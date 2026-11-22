using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aprobaciones.Api.Tests.Infra;

/// <summary>
/// Un navegador de un usuario: guarda sus cookies, toma el token XSRF de /api/me (como Angular)
/// y lo manda en cada escritura.
/// </summary>
public sealed class ClientePrueba
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private ClientePrueba(HttpClient http, Usuario usuario)
    {
        Http = http;
        Usuario = usuario;
    }

    public HttpClient Http { get; }
    public Usuario Usuario { get; }
    public string? TokenXsrf { get; private set; }

    public static async Task<ClientePrueba> CrearAsync(AprobacionesFactory factory, Usuario usuario, bool conXsrf)
    {
        var http = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        http.DefaultRequestHeaders.Add(AutenticacionPrueba.EncabezadoCuenta, usuario.Cuenta);
        http.DefaultRequestHeaders.Add(AutenticacionPrueba.EncabezadoGrupos, string.Join(',', usuario.Grupos));
        var cliente = new ClientePrueba(http, usuario);

        var me = await http.GetAsync("/api/me");
        me.EnsureSuccessStatusCode();
        cliente.TokenXsrf = me.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Select(c => c.Split(';')[0]).Where(c => c.StartsWith("XSRF-TOKEN=")).Select(c => Uri.UnescapeDataString(c["XSRF-TOKEN=".Length..])).FirstOrDefault()
            : null;
        if (conXsrf) cliente.UsarToken(cliente.TokenXsrf ?? throw new InvalidOperationException("/api/me no entregó la cookie XSRF-TOKEN."));
        return cliente;
    }

    public void UsarToken(string token)
    {
        Http.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Http.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
    }

    public Task<HttpResponseMessage> GetAsync(string url) => Http.GetAsync(url);
    public Task<HttpResponseMessage> DeleteAsync(string url) => Http.DeleteAsync(url);
    public Task<HttpResponseMessage> PostAsync(string url, object? cuerpo = null) =>
        cuerpo is null ? Http.PostAsync(url, null) : Http.PostAsJsonAsync(url, cuerpo, Json);
    public Task<HttpResponseMessage> PutAsync(string url, object cuerpo) => Http.PutAsJsonAsync(url, cuerpo, Json);

    public async Task<JsonNode> GetJsonAsync(string url)
    {
        var respuesta = await Http.GetAsync(url);
        await Esperar(respuesta, HttpStatusCode.OK);
        return JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!;
    }

    public async Task<JsonNode> DocumentoAsync(int id) => await GetJsonAsync($"/api/documentos/{id}");

    public async Task<int> CrearAsync(string descripcion)
    {
        var respuesta = await PostAsync("/api/documentos", new { descripcion });
        await Esperar(respuesta, HttpStatusCode.Created);
        return JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!["id"]!.GetValue<int>();
    }

    public Task<HttpResponseMessage> SubirAsync(int id, string tipo, byte[] contenido, string nombre)
    {
        var form = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue(tipo == "xml" ? "text/xml" : "application/pdf");
        form.Add(archivo, "archivo", nombre);
        return Http.PostAsync($"/api/documentos/{id}/archivos/{tipo}", form);
    }

    /// <summary>Crea un documento con XML y PDF; si se pide, lo envía a aprobación.</summary>
    public async Task<int> DocumentoListoAsync(decimal total, bool enviar = true, Guid? uuid = null, string descripcion = "Factura de prueba")
    {
        var id = await CrearAsync(descripcion);
        await Esperar(await SubirAsync(id, "xml", Cfdi.Xml(total, uuid: uuid), "factura.xml"), HttpStatusCode.OK);
        await Esperar(await SubirAsync(id, "pdf", Cfdi.Pdf, "factura.pdf"), HttpStatusCode.OK);
        if (enviar) await Esperar(await PostAsync($"/api/documentos/{id}/enviar"), HttpStatusCode.NoContent);
        return id;
    }

    public async Task AprobarAsync(int id, string? comentario = null) =>
        await Esperar(await PostAsync($"/api/documentos/{id}/aprobar", new { comentario }), HttpStatusCode.NoContent);

    /// <summary>Falla con el cuerpo de la respuesta a la vista, para entender el error sin depurar.</summary>
    public static async Task Esperar(HttpResponseMessage respuesta, HttpStatusCode esperado)
    {
        if (respuesta.StatusCode == esperado) return;
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        throw new Xunit.Sdk.XunitException($"{respuesta.RequestMessage?.Method} {respuesta.RequestMessage?.RequestUri?.PathAndQuery}: se esperaba {(int)esperado} y llegó {(int)respuesta.StatusCode}. {cuerpo}");
    }

    /// <summary>Detalle (ProblemDetails.detail) de una respuesta de error.</summary>
    public static async Task<string?> DetalleAsync(HttpResponseMessage respuesta) =>
        JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())?["detail"]?.GetValue<string>();
}

public static class Cfdi
{
    public static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n");

    /// <remarks>Montos con cultura invariante: en un equipo es-ES "1000.00" saldría "1000,00", que no es un CFDI válido.</remarks>
    public static byte[] Xml(decimal total, string receptor = AprobacionesFactory.RfcEmpresa, Guid? uuid = null, string moneda = "MXN", decimal? tipoCambio = null) =>
        Encoding.UTF8.GetBytes(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital" Version="4.0" Serie="IT" Folio="1" Fecha="2026-11-20T10:00:00" SubTotal="{total / 1.16m:0.00}" Total="{total:0.00}" Moneda="{moneda}"{(tipoCambio is { } tc ? $" TipoCambio=\"{tc}\"" : "")} TipoDeComprobante="I" Exportacion="01" LugarExpedicion="64000">
              <cfdi:Emisor Rfc="SME010101AB1" Nombre="PROVEEDOR &amp; ASOCIADOS" RegimenFiscal="601" />
              <cfdi:Receptor Rfc="{receptor}" Nombre="ESCUELA KEMPER URGATE" DomicilioFiscalReceptor="42501" RegimenFiscalReceptor="601" UsoCFDI="G03" />
              <cfdi:Complemento><tfd:TimbreFiscalDigital Version="1.1" UUID="{uuid ?? Guid.NewGuid()}" FechaTimbrado="2026-11-20T10:01:00" /></cfdi:Complemento>
            </cfdi:Comprobante>
            """));
}
