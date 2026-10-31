using Aprobaciones.Domain.Documentos;

namespace Aprobaciones.Application.Documentos;

public sealed record DocumentoResumenDto(
    int Id,
    string Folio,
    string Descripcion,
    EstadoDocumento Estado,
    string? NombreEmisor,
    string? RfcEmisor,
    decimal? Total,
    string? Moneda,
    decimal? MontoMxn,
    string? PasoActual,
    DateTime FechaCreacion,
    DateTime? FechaEnvio);

public sealed record DocumentoDetalleDto(
    int Id,
    string Folio,
    string Descripcion,
    EstadoDocumento Estado,
    UsuarioRefDto CreadoPor,
    DatosCfdi? Cfdi,
    decimal? MontoMxn,
    int Ronda,
    DateTime FechaCreacion,
    DateTime? FechaEnvio,
    DateTime? FechaAutorizacion,
    DateTime? FechaRechazo,
    DateTime? FechaProcesado,
    string? ReferenciaTesoreria,
    IReadOnlyList<ArchivoDto> Archivos,
    IReadOnlyList<AprobacionDto> Aprobaciones,
    IReadOnlyList<HistorialDto> Historial,
    PermisosDto Permisos);

public sealed record UsuarioRefDto(int Id, string Nombre);

public sealed record ArchivoDto(
    int Id, TipoArchivo Tipo, int Version, bool Vigente, string NombreOriginal, long TamanoBytes, DateTime FechaSubida);

public sealed record AprobacionDto(
    int Ronda, int Orden, string Nombre, EstadoPaso Estado, UsuarioRefDto? ResueltoPor, DateTime? FechaResolucion, string? Comentario);

public sealed record HistorialDto(
    AccionHistorial Accion, EstadoDocumento? EstadoAnterior, EstadoDocumento EstadoNuevo, UsuarioRefDto Usuario, string? Comentario, DateTime Fecha);

/// <summary>Acciones que la pantalla puede mostrar al usuario actual.</summary>
public sealed record PermisosDto(bool Editar, bool Enviar, bool Resolver, bool Procesar);

public sealed record Pagina<T>(IReadOnlyList<T> Elementos, int Total, int NumeroPagina, int TamanoPagina);

public sealed record CrearDocumentoRequest(string Descripcion);

public sealed record EditarDocumentoRequest(string Descripcion);

public sealed record AprobarRequest(string? Comentario);

public sealed record RechazarRequest(string Motivo);

public sealed record ProcesarRequest(string Referencia, string? Comentario);
