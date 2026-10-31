using Aprobaciones.Application;
using Aprobaciones.Application.Documentos;
using Aprobaciones.Domain;
using Aprobaciones.Domain.Documentos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Aprobaciones.Api.Endpoints;

internal static class DocumentosEndpoints
{
    public static IEndpointRouteBuilder MapDocumentosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/documentos");

        grupo.MapGet("/", (DocumentosService s, EstadoDocumento? estado, string? buscar, CancellationToken ct, int pagina = 1, int tamano = 20) =>
            s.ListarMisDocumentosAsync(estado, buscar, pagina, tamano, ct));

        grupo.MapGet("/{id:int}", (int id, DocumentosService s, CancellationToken ct) => s.ObtenerAsync(id, ct));

        grupo.MapPost("/", async (CrearDocumentoRequest request, DocumentosService s, CancellationToken ct) =>
        {
            var id = await s.CrearAsync(request, ct);
            return Results.Created($"/api/documentos/{id}", new { id });
        }).RequireAuthorization(Roles.Usuario);

        grupo.MapPut("/{id:int}", async (int id, EditarDocumentoRequest request, DocumentosService s, CancellationToken ct) =>
        {
            await s.EditarAsync(id, request, ct);
            return Results.NoContent();
        });

        // {tipo} = xml | pdf. El token antiforgery lo valida ValidacionAntiforgery (encabezado), no el de formularios.
        grupo.MapPost("/{id:int}/archivos/{tipo}", async (int id, string tipo, IFormFile archivo, DocumentosService s, CancellationToken ct) =>
        {
            var tipoArchivo = tipo.ToLowerInvariant() switch
            {
                "xml" => TipoArchivo.Xml,
                "pdf" => TipoArchivo.Pdf,
                _ => throw new DomainException("El tipo de archivo debe ser 'xml' o 'pdf'."),
            };
            await using var contenido = archivo.OpenReadStream();
            return Results.Ok(await s.SubirArchivoAsync(id, tipoArchivo, archivo.FileName, contenido, ct));
        }).DisableAntiforgery();

        grupo.MapGet("/{id:int}/archivos/{archivoId:int}", async (
            int id, int archivoId, bool? descargar, DocumentosService s, HttpContext contexto, CancellationToken ct) =>
        {
            var (archivo, contenido) = await s.AbrirArchivoAsync(id, archivoId, ct);
            // Contenido subido por usuarios: sin ejecución de scripts, sin adivinar el tipo y el XML siempre como descarga.
            contexto.Response.Headers.ContentSecurityPolicy = "sandbox";
            contexto.Response.Headers.XContentTypeOptions = "nosniff";
            var enLinea = archivo.Tipo == TipoArchivo.Pdf && descargar != true;
            var disposicion = new ContentDispositionHeaderValue(enLinea ? "inline" : "attachment");
            disposicion.SetHttpFileName(archivo.NombreOriginal);
            contexto.Response.Headers.ContentDisposition = disposicion.ToString();
            return Results.File(contenido, archivo.ContentType, enableRangeProcessing: true);
        });

        grupo.MapPost("/{id:int}/enviar", async (int id, DocumentosService s, CancellationToken ct) =>
        {
            await s.EnviarAsync(id, ct);
            return Results.NoContent();
        });

        grupo.MapPost("/{id:int}/aprobar", async (int id, [FromBody] AprobarRequest? request, DocumentosService s, CancellationToken ct) =>
        {
            await s.AprobarAsync(id, request ?? new AprobarRequest(null), ct);
            return Results.NoContent();
        }).RequireAuthorization(Roles.Aprobador);

        grupo.MapPost("/{id:int}/rechazar", async (int id, RechazarRequest request, DocumentosService s, CancellationToken ct) =>
        {
            await s.RechazarAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(Roles.Aprobador);

        grupo.MapPost("/{id:int}/procesar", async (int id, ProcesarRequest request, DocumentosService s, CancellationToken ct) =>
        {
            await s.ProcesarAsync(id, request, ct);
            return Results.NoContent();
        }).RequireAuthorization(Roles.Tesoreria);

        return app;
    }
}
