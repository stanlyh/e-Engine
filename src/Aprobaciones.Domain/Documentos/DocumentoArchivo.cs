namespace Aprobaciones.Domain.Documentos;

public sealed class DocumentoArchivo
{
    private DocumentoArchivo() { }

    internal DocumentoArchivo(TipoArchivo tipo, int version, ArchivoAlmacenado archivo, int subidoPorId, DateTime ahora)
    {
        Tipo = tipo;
        Version = version;
        NombreOriginal = archivo.NombreOriginal;
        RutaRelativa = archivo.RutaRelativa;
        ContentType = archivo.ContentType;
        TamanoBytes = archivo.TamanoBytes;
        HashSha256 = archivo.HashSha256;
        SubidoPorId = subidoPorId;
        FechaSubida = ahora;
        Vigente = true;
    }

    public int Id { get; private set; }
    public int DocumentoId { get; private set; }
    public TipoArchivo Tipo { get; private set; }
    public int Version { get; private set; }
    public bool Vigente { get; private set; }
    public string NombreOriginal { get; private set; } = "";
    public string RutaRelativa { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long TamanoBytes { get; private set; }
    public string HashSha256 { get; private set; } = "";
    public int SubidoPorId { get; private set; }
    public DateTime FechaSubida { get; private set; }

    internal void Reemplazar() => Vigente = false;
}

/// <summary>Metadatos de un archivo ya guardado en el almacén.</summary>
public sealed record ArchivoAlmacenado(
    string NombreOriginal, string RutaRelativa, string ContentType, long TamanoBytes, string HashSha256);
