namespace Aprobaciones.Domain.Documentos;

/// <summary>Paso de la cadena de aprobación copiado al documento en cada envío (ronda).</summary>
public sealed class DocumentoAprobacion
{
    private DocumentoAprobacion() { }

    internal DocumentoAprobacion(int ronda, PasoRuta paso, EstadoPaso estado)
    {
        Ronda = ronda;
        Orden = paso.Orden;
        Nombre = paso.Nombre;
        GrupoAd = paso.GrupoAd;
        Estado = estado;
    }

    public int Id { get; private set; }
    public int DocumentoId { get; private set; }
    public int Ronda { get; private set; }
    public int Orden { get; private set; }
    public string Nombre { get; private set; } = "";
    public string GrupoAd { get; private set; } = "";
    public EstadoPaso Estado { get; private set; }
    public int? ResueltoPorId { get; private set; }
    public DateTime? FechaResolucion { get; private set; }
    public string? Comentario { get; private set; }

    internal void Activar() => Estado = EstadoPaso.Pendiente;

    internal void Resolver(EstadoPaso estado, int usuarioId, string? comentario, DateTime ahora)
    {
        Estado = estado;
        ResueltoPorId = usuarioId;
        Comentario = comentario;
        FechaResolucion = ahora;
    }

    internal void Cancelar() => Estado = EstadoPaso.Cancelado;
}

/// <summary>Paso de una regla de aprobación, independiente de la persistencia.</summary>
public sealed record PasoRuta(int Orden, string Nombre, string GrupoAd);
