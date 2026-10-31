namespace Aprobaciones.Domain.Documentos;

/// <summary>
/// Agregado raíz. Todas las transiciones de estado pasan por aquí y cada una deja registro en el historial.
/// Borrador → EnAprobacion → (paso a paso) → EnTesoreria → Procesado; cualquier rechazo → Rechazado → reenviar.
/// </summary>
public sealed class Documento
{
    private readonly List<DocumentoArchivo> _archivos = [];
    private readonly List<DocumentoAprobacion> _aprobaciones = [];
    private readonly List<DocumentoHistorial> _historial = [];

    private Documento() { }

    public static Documento Crear(int usuarioId, string descripcion, DateTime ahora)
    {
        var documento = new Documento
        {
            CreadoPorId = usuarioId,
            Descripcion = ValidarDescripcion(descripcion),
            Estado = EstadoDocumento.Borrador,
            FechaCreacion = ahora,
        };
        documento.Registrar(AccionHistorial.Creado, null, usuarioId, null, ahora);
        return documento;
    }

    public int Id { get; private set; }
    /// <summary>Asignado por la base de datos (secuencia).</summary>
    public int Numero { get; private set; }
    /// <summary>Columna calculada: DOC-000123.</summary>
    public string Folio { get; private set; } = "";
    public string Descripcion { get; private set; } = "";
    public EstadoDocumento Estado { get; private set; }
    public int CreadoPorId { get; private set; }
    public DatosCfdi? Cfdi { get; private set; }
    public decimal? MontoMxn { get; private set; }
    /// <summary>Número de veces que se ha enviado a aprobación.</summary>
    public int Ronda { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public DateTime? FechaEnvio { get; private set; }
    public DateTime? FechaAutorizacion { get; private set; }
    public DateTime? FechaRechazo { get; private set; }
    public DateTime? FechaProcesado { get; private set; }
    public string? ReferenciaTesoreria { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<DocumentoArchivo> Archivos => _archivos;
    public IReadOnlyCollection<DocumentoAprobacion> Aprobaciones => _aprobaciones;
    public IReadOnlyCollection<DocumentoHistorial> Historial => _historial;

    public bool EsEditable => Estado is EstadoDocumento.Borrador or EstadoDocumento.Rechazado;

    public DocumentoArchivo? ArchivoVigente(TipoArchivo tipo) => _archivos.SingleOrDefault(a => a.Tipo == tipo && a.Vigente);

    public IEnumerable<DocumentoAprobacion> RondaActual => _aprobaciones.Where(a => a.Ronda == Ronda).OrderBy(a => a.Orden);

    public DocumentoAprobacion? PasoActual => _aprobaciones.SingleOrDefault(a => a.Ronda == Ronda && a.Estado == EstadoPaso.Pendiente);

    public void Editar(int usuarioId, string descripcion, DateTime ahora)
    {
        ExigirEdicionPorCreador(usuarioId);
        Descripcion = ValidarDescripcion(descripcion);
        Registrar(AccionHistorial.Editado, Estado, usuarioId, null, ahora);
    }

    /// <summary>Registra un archivo; si ya existía uno vigente del mismo tipo, queda como versión anterior.</summary>
    public DocumentoArchivo RegistrarArchivo(int usuarioId, TipoArchivo tipo, ArchivoAlmacenado archivo, DatosCfdi? cfdi, DateTime ahora)
    {
        ExigirEdicionPorCreador(usuarioId);
        if (tipo == TipoArchivo.Xml && cfdi is null) throw new DomainException("El XML debe traer los datos del CFDI.");
        if (tipo == TipoArchivo.Pdf && cfdi is not null) throw new DomainException("Los datos del CFDI solo vienen en el XML.");

        ArchivoVigente(tipo)?.Reemplazar();
        var version = _archivos.Where(a => a.Tipo == tipo).Select(a => a.Version).DefaultIfEmpty(0).Max() + 1;
        var nuevo = new DocumentoArchivo(tipo, version, archivo, usuarioId, ahora);
        _archivos.Add(nuevo);

        if (cfdi is not null)
        {
            Cfdi = cfdi;
            MontoMxn = cfdi.MontoMxn;
        }

        Registrar(AccionHistorial.ArchivoCargado, Estado, usuarioId, $"{tipo} v{version}: {archivo.NombreOriginal}", ahora);
        return nuevo;
    }

    /// <summary>Envía (o reenvía tras un rechazo) con la cadena de aprobación vigente para el monto.</summary>
    public void Enviar(int usuarioId, IReadOnlyList<PasoRuta> ruta, DateTime ahora)
    {
        ExigirEdicionPorCreador(usuarioId);
        if (ArchivoVigente(TipoArchivo.Xml) is null || ArchivoVigente(TipoArchivo.Pdf) is null)
            throw new DomainException("Se requieren el XML y el PDF para enviar.");
        if (Cfdi is null) throw new DomainException("El documento no tiene datos de CFDI.");
        if (ruta.Count == 0) throw new DomainException("La cadena de aprobación está vacía.");

        var esReenvio = Estado == EstadoDocumento.Rechazado;
        var anterior = Estado;
        var pasos = ruta.OrderBy(p => p.Orden).ToList();
        Ronda++;
        foreach (var paso in pasos)
            _aprobaciones.Add(new DocumentoAprobacion(Ronda, paso, EstadoPaso.EnEspera));
        RondaActual.First().Activar();

        Estado = EstadoDocumento.EnAprobacion;
        FechaEnvio = ahora;
        FechaRechazo = null;
        Registrar(esReenvio ? AccionHistorial.Reenviado : AccionHistorial.Enviado, anterior, usuarioId,
            $"Ruta: {string.Join(" → ", pasos.Select(p => p.Nombre))}", ahora);
    }

    /// <param name="perteneceAGrupo">Indica si el usuario es miembro de un grupo de AD.</param>
    public void Aprobar(int usuarioId, Func<string, bool> perteneceAGrupo, string? comentario, DateTime ahora)
    {
        var paso = ExigirPasoResoluble(usuarioId, perteneceAGrupo);
        paso.Resolver(EstadoPaso.Aprobado, usuarioId, Normalizar(comentario), ahora);

        var siguiente = RondaActual.FirstOrDefault(a => a.Estado == EstadoPaso.EnEspera);
        if (siguiente is not null)
        {
            siguiente.Activar();
            Registrar(AccionHistorial.PasoAprobado, Estado, usuarioId, Describir(paso, comentario), ahora);
            return;
        }

        var anterior = Estado;
        Estado = EstadoDocumento.EnTesoreria;
        FechaAutorizacion = ahora;
        Registrar(AccionHistorial.Autorizado, anterior, usuarioId, Describir(paso, comentario), ahora);
    }

    public void Rechazar(int usuarioId, Func<string, bool> perteneceAGrupo, string motivo, DateTime ahora)
    {
        var motivoNormalizado = Normalizar(motivo) ?? throw new DomainException("El motivo de rechazo es obligatorio.");
        var paso = ExigirPasoResoluble(usuarioId, perteneceAGrupo);
        paso.Resolver(EstadoPaso.Rechazado, usuarioId, motivoNormalizado, ahora);
        foreach (var pendiente in RondaActual.Where(a => a.Estado == EstadoPaso.EnEspera))
            pendiente.Cancelar();

        var anterior = Estado;
        Estado = EstadoDocumento.Rechazado;
        FechaRechazo = ahora;
        Registrar(AccionHistorial.Rechazado, anterior, usuarioId, Describir(paso, motivoNormalizado), ahora);
    }

    /// <summary>Cierre por Tesorería. El permiso de rol lo valida la capa de API.</summary>
    public void Procesar(int usuarioId, string referencia, string? comentario, DateTime ahora)
    {
        if (Estado != EstadoDocumento.EnTesoreria)
            throw new DomainException($"Solo se procesan documentos en Tesorería (estado actual: {Estado}).");
        ReferenciaTesoreria = Normalizar(referencia) ?? throw new DomainException("La referencia de Tesorería es obligatoria.");

        var anterior = Estado;
        Estado = EstadoDocumento.Procesado;
        FechaProcesado = ahora;
        Registrar(AccionHistorial.Procesado, anterior, usuarioId,
            Normalizar(comentario) is { } c ? $"Ref. {ReferenciaTesoreria}: {c}" : $"Ref. {ReferenciaTesoreria}", ahora);
    }

    /// <summary>Si el usuario puede aprobar o rechazar el paso actual (para mostrar u ocultar acciones).</summary>
    public bool PuedeResolver(int usuarioId, Func<string, bool> perteneceAGrupo) =>
        MotivoNoResoluble(usuarioId, perteneceAGrupo) is null;

    private DocumentoAprobacion ExigirPasoResoluble(int usuarioId, Func<string, bool> perteneceAGrupo)
    {
        if (MotivoNoResoluble(usuarioId, perteneceAGrupo) is { } motivo) throw new DomainException(motivo);
        return PasoActual!;
    }

    private string? MotivoNoResoluble(int usuarioId, Func<string, bool> perteneceAGrupo)
    {
        if (Estado != EstadoDocumento.EnAprobacion) return $"El documento no está en aprobación (estado actual: {Estado}).";
        if (usuarioId == CreadoPorId) return "No puedes aprobar ni rechazar tu propio documento.";
        if (PasoActual is not { } paso) return "El documento no tiene un paso pendiente.";
        if (!perteneceAGrupo(paso.GrupoAd)) return $"No perteneces al grupo que aprueba el paso '{paso.Nombre}'.";
        if (RondaActual.Any(a => a.Estado == EstadoPaso.Aprobado && a.ResueltoPorId == usuarioId))
            return "Ya aprobaste un paso de este documento; otra persona debe resolver este.";
        return null;
    }

    private void ExigirEdicionPorCreador(int usuarioId)
    {
        if (usuarioId != CreadoPorId) throw new DomainException("Solo quien creó el documento puede modificarlo.");
        if (!EsEditable) throw new DomainException($"El documento no se puede modificar en estado {Estado}.");
    }

    private void Registrar(AccionHistorial accion, EstadoDocumento? anterior, int usuarioId, string? comentario, DateTime ahora) =>
        _historial.Add(new DocumentoHistorial(accion, anterior, Estado, usuarioId, comentario, ahora));

    private static string Describir(DocumentoAprobacion paso, string? comentario) =>
        Normalizar(comentario) is { } c ? $"{paso.Nombre}: {c}" : paso.Nombre;

    private static string ValidarDescripcion(string descripcion) =>
        Normalizar(descripcion) is { Length: <= 500 } d ? d : throw new DomainException("La descripción es obligatoria (máx. 500 caracteres).");

    private static string? Normalizar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
