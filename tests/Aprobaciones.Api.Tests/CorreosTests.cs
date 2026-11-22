using System.Net;
using Aprobaciones.Api.Tests.Infra;
using Aprobaciones.Domain.Notificaciones;
using Microsoft.EntityFrameworkCore;
using static Aprobaciones.Api.Tests.Infra.ClientePrueba;

namespace Aprobaciones.Api.Tests;

/// <summary>
/// La cola de correos: reintentos ante un SMTP caído, destinos sin correo y directorio inaccesible.
/// Las pruebas comparten la cola, así que cada una revisa solo los avisos de sus documentos.
/// </summary>
[Collection("Correos en serie")]
public class CorreosTests(AprobacionesFactory api) : IClassFixture<AprobacionesFactory>
{
    private Task<List<Notificacion>> AvisosAsync(int documentoId) =>
        api.ConBaseAsync(db => db.Notificaciones.AsNoTracking().Where(n => n.DocumentoId == documentoId).OrderBy(n => n.Id).ToListAsync());

    /// <summary>Adelanta los reintentos programados, como si hubiera pasado el tiempo de espera.</summary>
    private Task AdelantarReintentosAsync() =>
        api.ConBaseAsync(db => db.Notificaciones.Where(n => n.Estado == EstadoNotificacion.Pendiente)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ProximoIntento, DateTime.UtcNow.AddSeconds(-1))));

    [Fact]
    public async Task Con_el_SMTP_caido_reintenta_con_espera_creciente_y_al_final_queda_fallida()
    {
        var creador = await api.ClienteAsync(Usuarios.Creador);
        var admin = await api.ClienteAsync(Usuarios.Admin);
        api.Correo.FallarCon = "Servidor SMTP no disponible";
        try
        {
            var id = await creador.DocumentoListoAsync(3_000m);

            await api.ProcesarCorreosAsync();
            var aviso = Assert.Single(await AvisosAsync(id));
            Assert.Equal((EstadoNotificacion.Pendiente, 1, "Servidor SMTP no disponible"), (aviso.Estado, aviso.Intentos, aviso.UltimoError));
            Assert.InRange(aviso.ProximoIntento - DateTime.UtcNow, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));

            // Antes de que venza la espera no se reintenta.
            await api.ProcesarCorreosAsync();
            Assert.Equal(1, Assert.Single(await AvisosAsync(id)).Intentos);

            for (var i = 0; i < Notificacion.Esperas.Length; i++)
            {
                await AdelantarReintentosAsync();
                await api.ProcesarCorreosAsync();
            }
            aviso = Assert.Single(await AvisosAsync(id));
            Assert.Equal((EstadoNotificacion.Fallida, Notificacion.Esperas.Length + 1), (aviso.Estado, aviso.Intentos));

            // Se recupera el servidor y Administración la reintenta.
            api.Correo.FallarCon = null;
            await Esperar(await admin.PostAsync($"/api/admin/correos/{aviso.Id}/reintentar"), HttpStatusCode.NoContent);
            await api.ProcesarCorreosAsync();
            aviso = Assert.Single(await AvisosAsync(id));
            Assert.Equal(EstadoNotificacion.Enviada, aviso.Estado);
            Assert.Equal("gabriela@empresa.test;gerardo@empresa.test", string.Join(';', aviso.Destinatarios!.Split(';').Order()));
        }
        finally
        {
            api.Correo.FallarCon = null;
        }
    }

    [Fact]
    public async Task Si_el_directorio_no_responde_se_reintenta_en_lugar_de_descartar()
    {
        var creador = await api.ClienteAsync(Usuarios.Creador);
        var id = await creador.DocumentoListoAsync(3_500m);
        api.Directorio.Disponible = false;
        try
        {
            await api.ProcesarCorreosAsync();
            var aviso = Assert.Single(await AvisosAsync(id));
            Assert.Equal(EstadoNotificacion.Pendiente, aviso.Estado);
            Assert.Contains("No se pudo consultar el grupo", aviso.UltimoError);
        }
        finally
        {
            api.Directorio.Disponible = true;
        }

        await AdelantarReintentosAsync();
        await api.ProcesarCorreosAsync();
        Assert.Equal(EstadoNotificacion.Enviada, Assert.Single(await AvisosAsync(id)).Estado);
    }

    [Fact]
    public async Task Un_grupo_sin_correos_queda_sin_destinatarios()
    {
        var creador = await api.ClienteAsync(Usuarios.Creador);
        var gerente = await api.ClienteAsync(Usuarios.Gerente1);
        var financiero = await api.ClienteAsync(Usuarios.Financiero);
        // Más de $250,000: el tercer paso es Director General, cuyo único miembro no tiene correo.
        var id = await creador.DocumentoListoAsync(300_000m);
        await gerente.AprobarAsync(id);
        await financiero.AprobarAsync(id);

        await api.ProcesarCorreosAsync();

        var director = (await AvisosAsync(id)).Last();
        Assert.Equal("APR_DirectorGeneral", director.GrupoAd);
        Assert.Equal(EstadoNotificacion.SinDestinatarios, director.Estado);
        Assert.Equal("Ningún miembro del grupo APR_DirectorGeneral tiene correo registrado.", director.UltimoError);
    }

    [Fact]
    public async Task El_contenido_del_usuario_va_codificado_en_el_HTML_y_el_enlace_lleva_a_la_bandeja()
    {
        var creador = await api.ClienteAsync(Usuarios.Creador);
        var gerente = await api.ClienteAsync(Usuarios.Gerente1);
        var id = await creador.DocumentoListoAsync(4_500m, descripcion: "<script>alert('x')</script> & más");
        await gerente.AprobarAsync(id, "Ok <b>sin</b> HTML");

        await api.ProcesarCorreosAsync();

        var folio = (await creador.DocumentoAsync(id))["folio"]!.GetValue<string>();
        var porAprobar = api.Correo.Enviados.Single(m => m.Asunto.StartsWith($"Por aprobar: {folio}"));
        // Ninguna etiqueta del usuario llega viva al HTML, y al decodificarlo se lee el texto original (los acentos van como &#225;).
        Assert.DoesNotContain("<script>", porAprobar.Html);
        Assert.Contains("&lt;script&gt;", porAprobar.Html);
        Assert.Contains("<script>alert('x')</script> & más", System.Net.WebUtility.HtmlDecode(porAprobar.Html));
        Assert.Contains($"https://aprobaciones.prueba/documentos/{id}?desde=aprobaciones", porAprobar.Html);
        var autorizado = api.Correo.Enviados.Single(m => m.Asunto.StartsWith($"Autorizado: {folio}"));
        Assert.Contains("Ok &lt;b&gt;sin&lt;/b&gt; HTML", autorizado.Html);
        Assert.DoesNotContain("<b>sin</b>", autorizado.Html);
        Assert.Contains("Ok <b>sin</b> HTML", autorizado.Texto);
    }

    [Fact]
    public async Task El_correo_de_prueba_llega_al_administrador()
    {
        var admin = await api.ClienteAsync(Usuarios.Admin);
        var respuesta = await admin.PostAsync("/api/admin/correos/prueba");
        await Esperar(respuesta, HttpStatusCode.OK);

        await api.ProcesarCorreosAsync();

        var prueba = api.Correo.Enviados.Last(m => m.Asunto.StartsWith("Correo de prueba"));
        Assert.Equal(["adriana@empresa.test"], prueba.Para);
        var resumen = await admin.GetJsonAsync("/api/admin/correos/resumen");
        Assert.Equal("Prueba", resumen["modo"]!.GetValue<string>());
        Assert.Equal("adriana@empresa.test", resumen["miCorreo"]!.GetValue<string>());
    }
}

/// <summary>Las pruebas de correos cambian el estado de los dobles (SMTP caído, directorio caído): no corren en paralelo entre sí.</summary>
[CollectionDefinition("Correos en serie", DisableParallelization = true)]
public class ColeccionCorreos;
