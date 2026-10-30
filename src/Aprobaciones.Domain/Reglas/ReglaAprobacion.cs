using Aprobaciones.Domain.Documentos;

namespace Aprobaciones.Domain.Reglas;

/// <summary>Rango de monto (MXN) y la cadena secuencial de aprobadores que le corresponde.</summary>
public sealed class ReglaAprobacion
{
    private readonly List<ReglaPaso> _pasos = [];

    private ReglaAprobacion() { }

    public ReglaAprobacion(string nombre, decimal montoDesde, decimal? montoHasta)
    {
        if (montoDesde < 0) throw new DomainException("MontoDesde no puede ser negativo.");
        if (montoHasta is not null && montoHasta < montoDesde)
            throw new DomainException("MontoHasta debe ser mayor o igual a MontoDesde.");
        Nombre = nombre;
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
    public IReadOnlyCollection<ReglaPaso> Pasos => _pasos;

    public bool Aplica(decimal monto) => Activa && monto >= MontoDesde && (MontoHasta is null || monto <= MontoHasta);

    public void AgregarPaso(string nombre, string grupoAd) =>
        _pasos.Add(new ReglaPaso(_pasos.Count + 1, nombre, grupoAd));

    public IReadOnlyList<PasoRuta> Ruta() =>
        [.. _pasos.OrderBy(p => p.Orden).Select(p => new PasoRuta(p.Orden, p.Nombre, p.GrupoAd))];

    /// <summary>Devuelve la cadena de aprobación para un monto; exige que exactamente una regla activa aplique.</summary>
    public static IReadOnlyList<PasoRuta> ResolverRuta(IEnumerable<ReglaAprobacion> reglas, decimal monto)
    {
        var aplicables = reglas.Where(r => r.Aplica(monto)).ToList();
        return aplicables switch
        {
            [] => throw new DomainException($"No hay regla de aprobación para el monto {monto:N2}."),
            [var unica] when unica._pasos.Count == 0 => throw new DomainException($"La regla '{unica.Nombre}' no tiene pasos."),
            [var unica] => unica.Ruta(),
            _ => throw new DomainException($"Hay {aplicables.Count} reglas que se traslapan para el monto {monto:N2}."),
        };
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
}
