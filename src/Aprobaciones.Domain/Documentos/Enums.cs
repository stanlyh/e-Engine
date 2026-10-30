namespace Aprobaciones.Domain.Documentos;

public enum EstadoDocumento : byte
{
    Borrador = 1,
    EnAprobacion = 2,
    Rechazado = 3,
    EnTesoreria = 4,
    Procesado = 5,
}

public enum TipoArchivo : byte
{
    Xml = 1,
    Pdf = 2,
}

public enum EstadoPaso : byte
{
    EnEspera = 1,
    Pendiente = 2,
    Aprobado = 3,
    Rechazado = 4,
    Cancelado = 5,
}

public enum AccionHistorial : byte
{
    Creado = 1,
    ArchivoCargado = 2,
    Enviado = 3,
    Reenviado = 4,
    PasoAprobado = 5,
    Autorizado = 6,
    Rechazado = 7,
    Procesado = 8,
    Editado = 9,
}
