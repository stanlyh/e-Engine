using System.Security.Cryptography;
using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Application.Documentos;
using Aprobaciones.Domain.Documentos;
using Microsoft.Extensions.Options;

namespace Aprobaciones.Infrastructure.Archivos;

/// <summary>Guarda los archivos en {RutaArchivos}/{año}/{mes}/{documentoId}/{guid}.{ext}.</summary>
internal sealed class AlmacenDisco(IOptions<OpcionesDocumentos> opciones, TimeProvider reloj) : IAlmacenArchivos
{
    private string RutaBase => Path.GetFullPath(opciones.Value.RutaArchivos);

    public async Task<ArchivoAlmacenado> GuardarAsync(
        int documentoId, string nombreOriginal, string extension, string contentType, byte[] contenido, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var rutaRelativa = string.Join('/',
            ahora.Year.ToString("0000"), ahora.Month.ToString("00"), documentoId.ToString(), $"{Guid.NewGuid():N}{extension}");
        var rutaCompleta = Resolver(rutaRelativa);

        Directory.CreateDirectory(Path.GetDirectoryName(rutaCompleta)!);
        await using (var destino = new FileStream(rutaCompleta, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await destino.WriteAsync(contenido, ct);

        var hash = Convert.ToHexStringLower(SHA256.HashData(contenido));
        return new ArchivoAlmacenado(nombreOriginal, rutaRelativa, contentType, contenido.LongLength, hash);
    }

    public Stream Abrir(string rutaRelativa) =>
        new FileStream(Resolver(rutaRelativa), FileMode.Open, FileAccess.Read, FileShare.Read);

    public void Eliminar(string rutaRelativa)
    {
        var ruta = Resolver(rutaRelativa);
        if (File.Exists(ruta)) File.Delete(ruta);
    }

    /// <summary>Convierte la ruta relativa en absoluta y garantiza que quede dentro de la carpeta base.</summary>
    private string Resolver(string rutaRelativa)
    {
        var baseConSeparador = Path.TrimEndingDirectorySeparator(RutaBase) + Path.DirectorySeparatorChar;
        var completa = Path.GetFullPath(Path.Combine(baseConSeparador, rutaRelativa));
        if (!completa.StartsWith(baseConSeparador, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Ruta de archivo fuera del almacén: {rutaRelativa}");
        return completa;
    }
}
