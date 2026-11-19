using Aprobaciones.Domain.Reglas;

namespace Aprobaciones.Domain.Tests;

public class ReglasAdministracionTests
{
    private static readonly DateTime Ahora = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    private static ReglaAprobacion Regla(string nombre, decimal desde, decimal? hasta, params string[] grupos)
    {
        var regla = new ReglaAprobacion(nombre, desde, hasta);
        foreach (var g in grupos.DefaultIfEmpty("APR_GerenteArea")) regla.AgregarPaso($"Paso {g}", g);
        return regla;
    }

    private static List<ReglaAprobacion> Validas() =>
    [
        Regla("Baja", 0m, 50_000m),
        Regla("Media", 50_000.01m, 250_000m, "APR_GerenteArea", "APR_GerenteFinanciero"),
        Regla("Alta", 250_000.01m, null, "APR_GerenteArea", "APR_GerenteFinanciero", "APR_DirectorGeneral"),
    ];

    [Fact]
    public void Las_reglas_iniciales_son_validas()
    {
        Assert.Empty(ReglaAprobacion.ValidarConjunto(Validas()));
    }

    [Fact]
    public void Una_sola_regla_abierta_desde_cero_es_valida()
    {
        Assert.Empty(ReglaAprobacion.ValidarConjunto([Regla("Todo", 0m, null)]));
    }

    [Fact]
    public void Detecta_hueco_entre_reglas_y_dice_que_montos_quedan_fuera()
    {
        var reglas = Validas();
        reglas[1] = Regla("Media", 60_000m, 250_000m);

        var error = Assert.Single(ReglaAprobacion.ValidarConjunto(reglas));
        Assert.Contains("50,000.01 a 59,999.99 no tienen regla", error);
    }

    [Fact]
    public void Detecta_traslape_aunque_sea_de_un_centavo()
    {
        var reglas = Validas();
        reglas[1] = Regla("Media", 50_000m, 250_000m);

        var error = Assert.Single(ReglaAprobacion.ValidarConjunto(reglas));
        Assert.Contains("se traslapan", error);
        Assert.Contains("50,000.01", error);
    }

    [Fact]
    public void La_primera_regla_debe_empezar_en_cero()
    {
        var reglas = Validas();
        reglas[0] = Regla("Baja", 1m, 50_000m);

        Assert.Contains(ReglaAprobacion.ValidarConjunto(reglas), e => e.Contains("$0.00 a 0.99"));
    }

    [Fact]
    public void La_ultima_regla_debe_quedar_abierta()
    {
        var reglas = Validas();
        reglas[2] = Regla("Alta", 250_000.01m, 1_000_000m);

        Assert.Contains(ReglaAprobacion.ValidarConjunto(reglas), e => e.Contains("mayores a 1,000,000.00"));
    }

    [Fact]
    public void Una_regla_abierta_antes_de_otra_se_reporta_como_traslape()
    {
        List<ReglaAprobacion> reglas = [Regla("Todo", 0m, null), Regla("Alta", 100m, null)];

        Assert.Contains(ReglaAprobacion.ValidarConjunto(reglas), e => e.Contains("no tiene límite superior"));
    }

    [Fact]
    public void Las_reglas_inactivas_no_cuentan_para_huecos_ni_traslapes()
    {
        var reglas = Validas();
        var inactiva = Regla("Vieja", 10m, 20m);
        inactiva.Actualizar("Vieja", 10m, 20m, activa: false, [("Paso", "APR_X")], 1, Ahora);
        reglas.Add(inactiva);

        Assert.Empty(ReglaAprobacion.ValidarConjunto(reglas));
    }

    [Fact]
    public void Sin_reglas_activas_es_error()
    {
        var unica = Regla("Todo", 0m, null);
        unica.Actualizar("Todo", 0m, null, activa: false, [("Paso", "APR_X")], 1, Ahora);

        Assert.Contains(ReglaAprobacion.ValidarConjunto([unica]), e => e.Contains("al menos una regla activa"));
    }

    [Fact]
    public void Los_nombres_no_se_repiten()
    {
        var reglas = Validas();
        reglas[1] = Regla("baja ", 50_000.01m, 250_000m);

        Assert.Contains(ReglaAprobacion.ValidarConjunto(reglas), e => e.Contains("se repite"));
    }

    [Theory]
    [InlineData(-1, null, "negativo")]
    [InlineData(100, 50.0, "mayor o igual")]
    [InlineData(0.001, null, "dos decimales")]
    public void Rechaza_rangos_invalidos(decimal desde, double? hasta, string mensaje)
    {
        var ex = Assert.Throws<DomainException>(() => new ReglaAprobacion("X", desde, (decimal?)hasta));
        Assert.Contains(mensaje, ex.Message);
    }

    [Fact]
    public void Actualizar_reemplaza_los_pasos_conservando_las_filas_por_posicion()
    {
        var regla = Regla("Media", 50_000.01m, 250_000m, "APR_A", "APR_B", "APR_C");
        var idsOriginales = regla.Pasos.OrderBy(p => p.Orden).ToList();

        var cambio = regla.Actualizar("Media", 50_000.01m, 250_000m, true, [("Nuevo 1", "APR_X"), ("Nuevo 2", "APR_Y")], 7, Ahora);

        Assert.True(cambio);
        var pasos = regla.Pasos.OrderBy(p => p.Orden).ToList();
        Assert.Equal([1, 2], pasos.Select(p => p.Orden));
        Assert.Equal(["APR_X", "APR_Y"], pasos.Select(p => p.GrupoAd));
        Assert.Same(idsOriginales[0], pasos[0]);
        Assert.Equal(7, regla.ModificadoPorId);
        Assert.Equal(Ahora, regla.FechaModificacion);
    }

    [Fact]
    public void Actualizar_sin_cambios_no_marca_modificacion()
    {
        var regla = Regla("Baja", 0m, 50_000m, "APR_A");

        Assert.False(regla.Actualizar(" Baja ", 0m, 50_000m, true, [("Paso APR_A", "APR_A")], 7, Ahora));
        Assert.Null(regla.ModificadoPorId);
    }

    [Fact]
    public void Un_grupo_no_puede_repetirse_en_la_cadena()
    {
        var regla = Regla("Baja", 0m, 50_000m);

        var ex = Assert.Throws<DomainException>(() =>
            regla.Actualizar("Baja", 0m, 50_000m, true, [("Uno", "APR_A"), ("Dos", "apr_a")], 1, Ahora));
        Assert.Contains("más de un paso", ex.Message);
    }

    [Fact]
    public void Una_regla_necesita_al_menos_un_paso()
    {
        var regla = Regla("Baja", 0m, 50_000m);

        Assert.Throws<DomainException>(() => regla.Actualizar("Baja", 0m, 50_000m, true, [], 1, Ahora));
    }
}
