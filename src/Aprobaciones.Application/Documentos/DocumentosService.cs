using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Domain;
using Aprobaciones.Domain.Documentos;
using Aprobaciones.Domain.Reglas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aprobaciones.Application.Documentos;

/// <summary>Casos de uso de documentos: carga, envío, resolución, cierre en Tesorería y consulta.</summary>
public sealed class DocumentosService(
    IAprobacionesDbContext db,
    IUsuarioActual usuario,
    IAlmacenArchivos almacen,
    ILectorCfdi lectorCfdi,
    IOptions<OpcionesDocumentos> opciones,
    TimeProvider reloj)
{
    private static readonly byte[] FirmaPdf = "%PDF-"u8.ToArray();

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    public async Task<int> CrearAsync(CrearDocumentoRequest request, CancellationToken ct)
    {
        var documento = Documento.Crear(usuario.Id, request.Descripcion, Ahora);
        db.Documentos.Add(documento);
        await db.SaveChangesAsync(ct);
        return documento.Id;
    }

    public async Task EditarAsync(int id, EditarDocumentoRequest request, CancellationToken ct)
    {
        var documento = await CargarParaModificarAsync(id, ct);
        documento.Editar(usuario.Id, request.Descripcion, Ahora);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Valida el archivo según su tipo (XML = CFDI 4.0, PDF por firma), lo guarda en disco y lo registra.</summary>
    public async Task<ArchivoDto> SubirArchivoAsync(
        int id, TipoArchivo tipo, string nombreOriginal, Stream contenido, CancellationToken ct)
    {
        var documento = await CargarParaModificarAsync(id, ct);
        var bytes = await LeerConLimiteAsync(contenido, opciones.Value.TamanoMaximoBytes, ct);
        var extension = Path.GetExtension(nombreOriginal).ToLowerInvariant();

        DatosCfdi? cfdi = null;
        string contentType;
        switch (tipo)
        {
            case TipoArchivo.Xml:
                if (extension != ".xml") throw new DomainException("El primer archivo debe ser el XML del CFDI.");
                cfdi = lectorCfdi.Leer(bytes);
                await ValidarCfdiAsync(documento.Id, cfdi, ct);
                contentType = "application/xml";
                break;
            case TipoArchivo.Pdf:
                if (extension != ".pdf" || !bytes.AsSpan().StartsWith(FirmaPdf))
                    throw new DomainException("El segundo archivo debe ser un PDF válido.");
                contentType = "application/pdf";
                break;
            default:
                throw new DomainException("Tipo de archivo no soportado.");
        }

        var almacenado = await almacen.GuardarAsync(documento.Id, Path.GetFileName(nombreOriginal), extension, contentType, bytes, ct);
        try
        {
            var archivo = documento.RegistrarArchivo(usuario.Id, tipo, almacenado, cfdi, Ahora);
            await db.SaveChangesAsync(ct);
            return AArchivoDto(archivo);
        }
        catch
        {
            almacen.Eliminar(almacenado.RutaRelativa);
            throw;
        }
    }

    public async Task EnviarAsync(int id, CancellationToken ct)
    {
        var documento = await CargarParaModificarAsync(id, ct);
        var monto = documento.MontoMxn ?? throw new DomainException("Carga el XML del CFDI antes de enviar.");
        var reglas = await db.ReglasAprobacion.Include(r => r.Pasos).Where(r => r.Activa).ToListAsync(ct);

        documento.Enviar(usuario.Id, ReglaAprobacion.ResolverRuta(reglas, monto), Ahora);
        await db.SaveChangesAsync(ct);
    }

    public async Task AprobarAsync(int id, AprobarRequest request, CancellationToken ct)
    {
        var documento = await CargarParaModificarAsync(id, ct);
        documento.Aprobar(usuario.Id, usuario.PerteneceAGrupo, request.Comentario, Ahora);
        await db.SaveChangesAsync(ct);
    }

    public async Task RechazarAsync(int id, RechazarRequest request, CancellationToken ct)
    {
        var documento = await CargarParaModificarAsync(id, ct);
        documento.Rechazar(usuario.Id, usuario.PerteneceAGrupo, request.Motivo, Ahora);
        await db.SaveChangesAsync(ct);
    }

    public async Task ProcesarAsync(int id, ProcesarRequest request, CancellationToken ct)
    {
        var documento = await CargarParaModificarAsync(id, ct);
        if (!usuario.EnRol(Roles.Tesoreria)) throw new AccesoDenegadoException("Solo Tesorería puede procesar documentos.");
        documento.Procesar(usuario.Id, request.Referencia, request.Comentario, Ahora);
        await db.SaveChangesAsync(ct);
    }

    public async Task<DocumentoDetalleDto> ObtenerAsync(int id, CancellationToken ct)
    {
        var documento = await db.Documentos
            .AsNoTracking()
            .Include(d => d.Archivos)
            .Include(d => d.Aprobaciones)
            .Include(d => d.Historial)
            .SingleOrDefaultAsync(d => d.Id == id, ct);
        documento = ExigirAcceso(documento, id);

        var idsUsuario = documento.Historial.Select(h => h.UsuarioId)
            .Concat(documento.Aprobaciones.Where(a => a.ResueltoPorId is not null).Select(a => a.ResueltoPorId!.Value))
            .Append(documento.CreadoPorId)
            .Distinct()
            .ToList();
        var nombres = await db.Usuarios.AsNoTracking()
            .Where(u => idsUsuario.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);
        UsuarioRefDto Ref(int uid) => new(uid, nombres.GetValueOrDefault(uid, "(desconocido)"));

        var esCreador = documento.CreadoPorId == usuario.Id;
        var permisos = new PermisosDto(
            Editar: esCreador && documento.EsEditable,
            Enviar: esCreador && documento.EsEditable
                && documento.ArchivoVigente(TipoArchivo.Xml) is not null && documento.ArchivoVigente(TipoArchivo.Pdf) is not null,
            Resolver: documento.PuedeResolver(usuario.Id, usuario.PerteneceAGrupo),
            Procesar: documento.Estado == EstadoDocumento.EnTesoreria && usuario.EnRol(Roles.Tesoreria));

        return new DocumentoDetalleDto(
            documento.Id,
            documento.Folio,
            documento.Descripcion,
            documento.Estado,
            Ref(documento.CreadoPorId),
            documento.Cfdi,
            documento.MontoMxn,
            documento.Ronda,
            documento.FechaCreacion,
            documento.FechaEnvio,
            documento.FechaAutorizacion,
            documento.FechaRechazo,
            documento.FechaProcesado,
            documento.ReferenciaTesoreria,
            [.. documento.Archivos.OrderBy(a => a.Tipo).ThenByDescending(a => a.Version).Select(AArchivoDto)],
            [.. documento.Aprobaciones.OrderBy(a => a.Ronda).ThenBy(a => a.Orden).Select(a => new AprobacionDto(
                a.Ronda, a.Orden, a.Nombre, a.Estado,
                a.ResueltoPorId is { } r ? Ref(r) : null, a.FechaResolucion, a.Comentario))],
            [.. documento.Historial.OrderBy(h => h.Fecha).ThenBy(h => h.Id).Select(h => new HistorialDto(
                h.Accion, h.EstadoAnterior, h.EstadoNuevo, Ref(h.UsuarioId), h.Comentario, h.Fecha))],
            permisos);
    }

    /// <summary>Documentos creados por el usuario actual, del más reciente al más antiguo.</summary>
    public async Task<Pagina<DocumentoResumenDto>> ListarMisDocumentosAsync(
        EstadoDocumento? estado, string? buscar, int numeroPagina, int tamanoPagina, CancellationToken ct)
    {
        numeroPagina = Math.Max(1, numeroPagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 100);

        var consulta = db.Documentos.AsNoTracking().Where(d => d.CreadoPorId == usuario.Id);
        if (estado is not null) consulta = consulta.Where(d => d.Estado == estado);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim();
            consulta = Guid.TryParse(texto, out var uuid)
                ? consulta.Where(d => d.Cfdi!.Uuid == uuid)
                : consulta.Where(d => d.Folio.Contains(texto) || d.Descripcion.Contains(texto)
                    || d.Cfdi!.NombreEmisor.Contains(texto) || d.Cfdi.RfcEmisor.Contains(texto));
        }

        var total = await consulta.CountAsync(ct);
        var elementos = await consulta
            .OrderByDescending(d => d.FechaCreacion)
            .Skip((numeroPagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(d => new DocumentoResumenDto(
                d.Id, d.Folio, d.Descripcion, d.Estado,
                d.Cfdi!.NombreEmisor, d.Cfdi.RfcEmisor, d.Cfdi.Total, d.Cfdi.Moneda, d.MontoMxn,
                d.Aprobaciones.Where(a => a.Ronda == d.Ronda && a.Estado == EstadoPaso.Pendiente).Select(a => a.Nombre).FirstOrDefault(),
                d.FechaCreacion, d.FechaEnvio))
            .ToListAsync(ct);

        return new Pagina<DocumentoResumenDto>(elementos, total, numeroPagina, tamanoPagina);
    }

    public async Task<(DocumentoArchivo Archivo, Stream Contenido)> AbrirArchivoAsync(int id, int archivoId, CancellationToken ct)
    {
        var documento = await db.Documentos.AsNoTracking()
            .Include(d => d.Archivos)
            .Include(d => d.Aprobaciones)
            .SingleOrDefaultAsync(d => d.Id == id, ct);
        documento = ExigirAcceso(documento, id);

        var archivo = documento.Archivos.SingleOrDefault(a => a.Id == archivoId)
            ?? throw new NoEncontradoException($"El archivo {archivoId} no existe.");
        return (archivo, almacen.Abrir(archivo.RutaRelativa));
    }

    private async Task<Documento> CargarParaModificarAsync(int id, CancellationToken ct)
    {
        var documento = await db.Documentos
            .Include(d => d.Archivos)
            .Include(d => d.Aprobaciones)
            .SingleOrDefaultAsync(d => d.Id == id, ct);
        return ExigirAcceso(documento, id);
    }

    /// <summary>
    /// Pueden ver un documento: quien lo creó, Admin, Tesorería cuando ya llegó a ella,
    /// y los miembros de los grupos de su cadena de aprobación.
    /// </summary>
    private Documento ExigirAcceso(Documento? documento, int id)
    {
        var puedeVer = documento is not null && (
            documento.CreadoPorId == usuario.Id
            || usuario.EnRol(Roles.Admin)
            || (usuario.EnRol(Roles.Tesoreria) && documento.Estado is EstadoDocumento.EnTesoreria or EstadoDocumento.Procesado)
            || documento.Aprobaciones.Any(a => usuario.PerteneceAGrupo(a.GrupoAd)));
        return puedeVer ? documento! : throw new NoEncontradoException($"El documento {id} no existe.");
    }

    private async Task ValidarCfdiAsync(int documentoId, DatosCfdi cfdi, CancellationToken ct)
    {
        var config = opciones.Value;
        if (!config.TiposComprobante.Contains(cfdi.TipoComprobante, StringComparer.OrdinalIgnoreCase))
            throw new DomainException($"El tipo de comprobante '{cfdi.TipoComprobante}' no se acepta (permitidos: {string.Join(", ", config.TiposComprobante)}).");
        if (!string.IsNullOrWhiteSpace(config.RfcEmpresa)
            && !string.Equals(cfdi.RfcReceptor, config.RfcEmpresa.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new DomainException($"La factura está emitida al RFC {cfdi.RfcReceptor}, no al de la empresa.");

        var duplicado = await db.Documentos.AsNoTracking()
            .Where(d => d.Id != documentoId && d.Cfdi!.Uuid == cfdi.Uuid)
            .Select(d => d.Folio)
            .FirstOrDefaultAsync(ct);
        if (duplicado is not null)
            throw new DomainException($"La factura {cfdi.Uuid} ya está registrada en el documento {duplicado}.");
    }

    private static async Task<byte[]> LeerConLimiteAsync(Stream contenido, long limite, CancellationToken ct)
    {
        using var memoria = new MemoryStream();
        var buffer = new byte[81920];
        int leidos;
        while ((leidos = await contenido.ReadAsync(buffer, ct)) > 0)
        {
            if (memoria.Length + leidos > limite)
                throw new DomainException($"El archivo excede el tamaño máximo de {limite / 1024 / 1024} MB.");
            memoria.Write(buffer, 0, leidos);
        }
        if (memoria.Length == 0) throw new DomainException("El archivo está vacío.");
        return memoria.ToArray();
    }

    private static ArchivoDto AArchivoDto(DocumentoArchivo a) =>
        new(a.Id, a.Tipo, a.Version, a.Vigente, a.NombreOriginal, a.TamanoBytes, a.FechaSubida);
}
