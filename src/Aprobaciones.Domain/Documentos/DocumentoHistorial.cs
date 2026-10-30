namespace Aprobaciones.Domain.Documentos;

public sealed class DocumentoHistorial
{
    private DocumentoHistorial() { }

    internal DocumentoHistorial(
        AccionHistorial accion, EstadoDocumento? anterior, EstadoDocumento nuevo, int usuarioId, string? comentario, DateTime ahora)
    {
        Accion = accion;
        EstadoAnterior = anterior;
        EstadoNuevo = nuevo;
        UsuarioId = usuarioId;
        Comentario = comentario;
        Fecha = ahora;
    }

    public long Id { get; private set; }
    public int DocumentoId { get; private set; }
    public AccionHistorial Accion { get; private set; }
    public EstadoDocumento? EstadoAnterior { get; private set; }
    public EstadoDocumento EstadoNuevo { get; private set; }
    public int UsuarioId { get; private set; }
    public string? Comentario { get; private set; }
    public DateTime Fecha { get; private set; }
}
