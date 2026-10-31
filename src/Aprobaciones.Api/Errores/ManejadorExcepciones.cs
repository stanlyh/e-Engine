using Aprobaciones.Application;
using Aprobaciones.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Aprobaciones.Api.Errores;

/// <summary>Traduce errores de negocio, permisos y concurrencia a ProblemDetails.</summary>
internal sealed class ManejadorExcepciones(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken cancellationToken)
    {
        (int estado, string titulo, string detalle)? respuesta = excepcion switch
        {
            DomainException e => (StatusCodes.Status400BadRequest, "Regla de negocio", e.Message),
            NoEncontradoException e => (StatusCodes.Status404NotFound, "No encontrado", e.Message),
            AccesoDenegadoException e => (StatusCodes.Status403Forbidden, "Acceso denegado", e.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflicto",
                "Otra persona modificó el documento al mismo tiempo. Recarga e intenta de nuevo."),
            // Respaldo del índice único: dos cargas simultáneas de la misma factura.
            DbUpdateException e when e.InnerException?.Message.Contains("IX_Documentos_CfdiUuid") == true =>
                (StatusCodes.Status409Conflict, "Factura duplicada", "Esta factura (UUID) ya está registrada en otro documento."),
            BadHttpRequestException e => (e.StatusCode, "Solicitud inválida", e.Message),
            _ => null,
        };
        if (respuesta is not { } r) return false;

        contexto.Response.StatusCode = r.estado;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excepcion,
            ProblemDetails = { Status = r.estado, Title = r.titulo, Detail = r.detalle },
        });
    }
}
