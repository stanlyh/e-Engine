using Aprobaciones.Domain.Notificaciones;

namespace Aprobaciones.Domain.Tests;

public class NotificacionTests
{
    private static readonly DateTime Ahora = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    private static Notificacion Nueva() =>
        Notificacion.ParaGrupo(EventoNotificacion.PorAprobar, 1, "APR_GerenteArea", ["DOM\\ana", "dom\\ANA", "", "DOM\\luis"], "Asunto", "<p>x</p>", "x", Ahora);

    [Fact]
    public void Nace_pendiente_para_enviarse_de_inmediato_y_sin_cuentas_duplicadas()
    {
        var n = Nueva();

        Assert.Equal(EstadoNotificacion.Pendiente, n.Estado);
        Assert.Equal(Ahora, n.ProximoIntento);
        Assert.Equal(["DOM\\ana", "DOM\\luis"], n.CuentasExcluidas);
    }

    [Fact]
    public void Cada_fallo_espera_mas_y_al_agotar_los_reintentos_queda_fallida()
    {
        var n = Nueva();
        var momento = Ahora;

        foreach (var espera in Notificacion.Esperas)
        {
            n.RegistrarFallo("SMTP caído", ["a@x.mx"], momento);
            Assert.Equal(EstadoNotificacion.Pendiente, n.Estado);
            Assert.Equal(momento + espera, n.ProximoIntento);
            momento = n.ProximoIntento;
        }

        n.RegistrarFallo("SMTP caído", ["a@x.mx"], momento);
        Assert.Equal(EstadoNotificacion.Fallida, n.Estado);
        Assert.Equal(Notificacion.Esperas.Length + 1, n.Intentos);
        Assert.Equal("SMTP caído", n.UltimoError);
    }

    [Fact]
    public void Reintentar_solo_aplica_a_fallidas_o_sin_destinatarios()
    {
        var n = Nueva();
        Assert.Throws<DomainException>(() => n.Reintentar(Ahora));

        n.MarcarSinDestinatarios("Nadie tiene correo", Ahora);
        n.Reintentar(Ahora.AddHours(1));

        Assert.Equal(EstadoNotificacion.Pendiente, n.Estado);
        Assert.Equal(0, n.Intentos);
        Assert.Equal(Ahora.AddHours(1), n.ProximoIntento);
    }

    [Fact]
    public void Al_enviarse_guarda_los_destinatarios_y_limpia_el_error()
    {
        var n = Nueva();
        n.RegistrarFallo("timeout", [], Ahora);

        n.MarcarEnviada(["a@x.mx", "b@x.mx"], Ahora.AddMinutes(2));

        Assert.Equal(EstadoNotificacion.Enviada, n.Estado);
        Assert.Equal("a@x.mx;b@x.mx", n.Destinatarios);
        Assert.Null(n.UltimoError);
        Assert.Null(n.BloqueadaHasta);
    }

    [Fact]
    public void Un_destino_de_grupo_requiere_el_grupo()
    {
        Assert.Throws<DomainException>(() =>
            Notificacion.ParaGrupo(EventoNotificacion.PorAprobar, 1, " ", [], "Asunto", "", "", Ahora));
    }
}
