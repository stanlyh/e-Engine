using Aprobaciones.Domain.Documentos;

namespace Aprobaciones.Application.Abstracciones;

/// <summary>Almacén físico de los archivos cargados; la base de datos solo guarda la ruta relativa.</summary>
public interface IAlmacenArchivos
{
    Task<ArchivoAlmacenado> GuardarAsync(
        int documentoId, string nombreOriginal, string extension, string contentType, byte[] contenido, CancellationToken ct);

    Stream Abrir(string rutaRelativa);

    void Eliminar(string rutaRelativa);
}

/// <summary>Lee y valida un XML CFDI 4.0 timbrado.</summary>
public interface ILectorCfdi
{
    /// <exception cref="Domain.DomainException">Si el XML no es un CFDI 4.0 válido y timbrado.</exception>
    DatosCfdi Leer(byte[] xml);
}
