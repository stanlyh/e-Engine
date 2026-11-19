using System.Globalization;
using Aprobaciones.Domain.Documentos;

namespace Aprobaciones.Domain.Reglas;

/// <summary>Rango de monto (MXN) y la cadena secuencial de aprobadores que le corresponde.</summary>
public sealed class ReglaAprobacion
{
    public const int MaximoPasos = 10;
    /// <summary>Los montos se comparan en centavos: la siguiente regla empieza un centavo después de donde termina la anterior.</summary>
    public const decimal Centavo = 0.01m;

    private readonly List<ReglaPaso> _pasos = [];

    private ReglaAprobacion() { }

    public ReglaAprobacion(string nombre, decimal montoDesde, decimal? montoHasta)
    {
        ValidarRango(montoDesde, montoHasta);
        Nombre = ValidarTexto(nombre, "El nombre de la regla", 100);
        MontoDesde = montoDesde;
        MontoHasta = montoHasta;
        Activa = true;
    }

    public int Id { get; private set; }
    public string Nombre { get; private set; } = "";
    public decimal MontoDesde { get; private set; }
    /// <summary>null = sin límite superior.</summary>
    public decimal? MontoHasta { get; private set; }
    public bool Activa { get; private set; }
    public int? ModificadoPorId { get; private set; }
    public DateTime? FechaModificacion { get; private set; }
    public IReadOnlyCollection<ReglaPaso> Pasos => _pasos;

    public bool Aplica(decimal monto) => Activa && monto >= MontoDesde && (MontoHasta is null || monto <= MontoHasta);

    public void AgregarPaso(string nombre, string grupoAd) =>
        _pasos.Add(new ReglaPaso(_pasos.Count + 1, ValidarTexto(nombre, "El nombre del paso", 100), ValidarTexto(grupoAd, "El grupo de AD", 256)));

    /// <summary>
    /// Aplica los cambios de la pantalla de administración. Los pasos existentes se actualizan por posición
    /// (evita choques con el índice único de Orden al guardar). Devuelve si hubo cambios.
    /// </summary>
    public bool Actualizar(
        string nombre, decimal montoDesde, decimal? montoHasta, bool activa,
        IReadOnlyList<(string Nombre, string GrupoAd)> pasos, int usuarioId, DateTime ahora)
    {
        ValidarRango(montoDesde, montoHasta);
        var nombreValido = ValidarTexto(nombre, "El nombre de la regla", 100);
        var pasosValidos = ValidarPasos(nombreValido, pasos);

        var ordenados = _pasos.OrderBy(p => p.Orden).ToList();
        var cambio = Nombre != nombreValido || MontoDesde != montoDesde || MontoHasta != montoHasta || Activa != activa
            || ordenados.Count != pasosValidos.Count
            || ordenados.Zip(pasosValidos).Any(x => x.First.Nombre != x.Second.Nombre || x.First.GrupoAd != x.Second.GrupoAd);
        if (!cambio) return false;

        Nombre = nombreValido;
        MontoDesde = montoDesde;
        MontoHasta = montoHasta;
        Activa = activa;
        for (var i = 0; i < pasosValidos.Count; i++)
        {
            if (i < ordenados.Count) ordenados[i].Cambiar(pasosValidos[i].Nombre, pasosValidos[i].GrupoAd);
            else _pasos.Add(new ReglaPaso(i + 1, pasosValidos[i].Nombre, pasosValidos[i].GrupoAd));
        }
        foreach (var sobrante in ordenados.Skip(pasosValidos.Count)) _pasos.Remove(sobrante);

        ModificadoPorId = usuarioId;
        FechaModificacion = ahora;
        return true;
    }

    /// <summary>Marca una regla nueva con quién la creó.</summary>
    public void RegistrarAlta(int usuarioId, DateTime ahora)
    {
        ValidarPasos(Nombre, [.. _pasos.OrderBy(p => p.Orden).Select(p => (p.Nombre, p.GrupoAd))]);
        ModificadoPorId = usuarioId;
        FechaModificacion = ahora;
    }

    public IReadOnlyList<PasoRuta> Ruta() =>
        [.. _pasos.OrderBy(p => p.Orden).Select(p => new PasoRuta(p.Orden, p.Nombre, p.GrupoAd))];

    /// <summary>Devuelve la cadena de aprobación para un monto; exige que exactamente una regla activa aplique.</summary>
    public static IReadOnlyList<PasoRuta> ResolverRuta(IEnumerable<ReglaAprobacion> reglas, decimal monto)
    {
        var aplicables = reglas.Where(r => r.Aplica(monto)).ToList();
        return aplicables switch
        {
            [] => throw new DomainException($"No hay regla de aprobación para el monto {M(monto)}."),
            [var unica] when unica._pasos.Count == 0 => throw new DomainException($"La regla '{unica.Nombre}' no tiene pasos."),
            [var unica] => unica.Ruta(),
            _ => throw new DomainException($"Hay {aplicables.Count} reglas que se traslapan para el monto {M(monto)}."),
        };
    }

    /// <summary>
    /// Revisa que las reglas activas cubran todos los montos, de $0 en adelante, sin huecos ni traslapes,
    /// para que cualquier documento encuentre exactamente una cadena al enviarse. Devuelve los problemas encontrados.
    /// </summary>
    public static IReadOnlyList<string> ValidarConjunto(IEnumerable<ReglaAprobacion> reglas)
    {
        var lista = reglas.ToList();
        var errores = new List<string>();

        foreach (var repetido in lista.GroupBy(r => r.Nombre.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errores.Add($"El nombre '{repetido.Key}' se repite; cada regla debe tener un nombre distinto.");

        var activas = lista.Where(r => r.Activa).OrderBy(r => r.MontoDesde).ThenBy(r => r.MontoHasta ?? decimal.MaxValue).ToList();
        if (activas.Count == 0)
        {
            errores.Add("Debe haber al menos una regla activa; sin ella ningún documento se puede enviar.");
            return errores;
        }

        foreach (var sinPasos in activas.Where(r => r._pasos.Count == 0))
            errores.Add($"La regla '{sinPasos.Nombre}' no tiene pasos.");

        if (activas[0].MontoDesde != 0)
            errores.Add($"Los montos de $0.00 a {M(activas[0].MontoDesde - Centavo)} no tienen regla: la primera regla activa debe empezar en $0.");

        for (var i = 1; i < activas.Count; i++)
        {
            var anterior = activas[i - 1];
            var actual = activas[i];
            if (anterior.MontoHasta is not { } hasta)
            {
                errores.Add($"'{anterior.Nombre}' no tiene límite superior y se traslapa con '{actual.Nombre}'.");
                continue;
            }
            var esperado = hasta + Centavo;
            if (actual.MontoDesde < esperado)
                errores.Add($"'{anterior.Nombre}' y '{actual.Nombre}' se traslapan: '{actual.Nombre}' debe empezar en {M(esperado)}.");
            else if (actual.MontoDesde > esperado)
                errores.Add($"Los montos de {M(esperado)} a {M(actual.MontoDesde - Centavo)} no tienen regla (entre '{anterior.Nombre}' y '{actual.Nombre}').");
        }

        if (activas[^1].MontoHasta is { } tope)
            errores.Add($"Los montos mayores a {M(tope)} no tienen regla: la última regla activa debe quedar sin límite superior.");

        return errores;
    }

    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

    /// <summary>Montos en los mensajes con formato de México (50,000.01), sin depender del idioma del servidor.</summary>
    private static string M(decimal monto) => monto.ToString("N2", Mx);

    private static string M(decimal? monto) => monto is { } m ? M(m) : "sin límite";

    private static void ValidarRango(decimal montoDesde, decimal? montoHasta)
    {
        if (montoDesde < 0) throw new DomainException("El monto inicial no puede ser negativo.");
        if (decimal.Round(montoDesde, 2) != montoDesde || (montoHasta is { } h && decimal.Round(h, 2) != h))
            throw new DomainException("Los montos admiten como máximo dos decimales.");
        if (montoHasta is not null && montoHasta < montoDesde)
            throw new DomainException($"El monto final ({M(montoHasta)}) debe ser mayor o igual al inicial ({M(montoDesde)}).");
    }

    private static List<(string Nombre, string GrupoAd)> ValidarPasos(string regla, IReadOnlyList<(string Nombre, string GrupoAd)> pasos)
    {
        if (pasos.Count == 0) throw new DomainException($"La regla '{regla}' debe tener al menos un paso.");
        if (pasos.Count > MaximoPasos) throw new DomainException($"La regla '{regla}' admite como máximo {MaximoPasos} pasos.");
        var validos = pasos.Select(p => (ValidarTexto(p.Nombre, "El nombre del paso", 100), ValidarTexto(p.GrupoAd, "El grupo de AD", 256))).ToList();
        // Nadie aprueba dos pasos del mismo documento: un grupo repetido exigiría a dos personas del mismo grupo.
        if (validos.GroupBy(p => p.Item2, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } repetido)
            throw new DomainException($"En la regla '{regla}' el grupo '{repetido.Key}' aparece en más de un paso.");
        return validos;
    }

    private static string ValidarTexto(string? texto, string campo, int maximo)
    {
        var limpio = texto?.Trim();
        if (string.IsNullOrEmpty(limpio)) throw new DomainException($"{campo} es obligatorio.");
        if (limpio.Length > maximo) throw new DomainException($"{campo} admite como máximo {maximo} caracteres.");
        return limpio;
    }
}

public sealed class ReglaPaso
{
    private ReglaPaso() { }

    internal ReglaPaso(int orden, string nombre, string grupoAd)
    {
        Orden = orden;
        Nombre = nombre;
        GrupoAd = grupoAd;
    }

    public int Id { get; private set; }
    public int ReglaAprobacionId { get; private set; }
    public int Orden { get; private set; }
    public string Nombre { get; private set; } = "";
    /// <summary>Grupo de AD cuyos miembros pueden aprobar este paso (DOMINIO\Grupo).</summary>
    public string GrupoAd { get; private set; } = "";

    internal void Cambiar(string nombre, string grupoAd)
    {
        Nombre = nombre;
        GrupoAd = grupoAd;
    }
}
