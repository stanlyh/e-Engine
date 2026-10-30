using Aprobaciones.Domain;
using Aprobaciones.Domain.Reglas;

namespace Aprobaciones.Domain.Tests;

public class ReglaAprobacionTests
{
    private static List<ReglaAprobacion> ReglasEjemplo()
    {
        var baja = new ReglaAprobacion("Hasta 50 mil", 0m, 50_000m);
        baja.AgregarPaso("Gerente de Área", "APR_GerenteArea");

        var media = new ReglaAprobacion("50 mil a 250 mil", 50_000.01m, 250_000m);
        media.AgregarPaso("Gerente de Área", "APR_GerenteArea");
        media.AgregarPaso("Gerente Financiero", "APR_GerenteFinanciero");

        var alta = new ReglaAprobacion("Más de 250 mil", 250_000.01m, null);
        alta.AgregarPaso("Gerente de Área", "APR_GerenteArea");
        alta.AgregarPaso("Gerente Financiero", "APR_GerenteFinanciero");
        alta.AgregarPaso("Director General", "APR_DirectorGeneral");

        return [baja, media, alta];
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50_000, 1)]
    [InlineData(50_000.01, 2)]
    [InlineData(250_000, 2)]
    [InlineData(250_000.01, 3)]
    [InlineData(10_000_000, 3)]
    public void Resuelve_la_cadena_segun_el_monto(decimal monto, int pasosEsperados)
    {
        var ruta = ReglaAprobacion.ResolverRuta(ReglasEjemplo(), monto);

        Assert.Equal(pasosEsperados, ruta.Count);
        Assert.Equal(Enumerable.Range(1, pasosEsperados), ruta.Select(p => p.Orden));
    }

    [Fact]
    public void Falla_si_ninguna_regla_aplica()
    {
        var reglas = ReglasEjemplo().Take(1);

        Assert.Throws<DomainException>(() => ReglaAprobacion.ResolverRuta(reglas, 60_000m));
    }

    [Fact]
    public void Falla_si_las_reglas_se_traslapan()
    {
        var reglas = ReglasEjemplo();
        var traslape = new ReglaAprobacion("Traslape", 40_000m, 60_000m);
        traslape.AgregarPaso("X", "X");
        reglas.Add(traslape);

        Assert.Throws<DomainException>(() => ReglaAprobacion.ResolverRuta(reglas, 45_000m));
    }
}
