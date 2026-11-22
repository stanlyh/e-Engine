using System.Net;
using System.Text.Json.Nodes;
using Aprobaciones.Api.Tests.Infra;
using static Aprobaciones.Api.Tests.Infra.ClientePrueba;

namespace Aprobaciones.Api.Tests;

/// <summary>Administración de reglas. Cada prueba deja las reglas como estaban porque comparten la base.</summary>
public class ReglasTests(AprobacionesFactory api) : IClassFixture<AprobacionesFactory>
{
    private static object Edicion(JsonNode conjunto, Action<JsonArray>? cambiar = null)
    {
        var reglas = new JsonArray([.. conjunto["reglas"]!.AsArray().Select(r => (JsonNode)new JsonObject
        {
            ["id"] = r!["id"]!.GetValue<int>(),
            ["nombre"] = r["nombre"]!.GetValue<string>(),
            ["montoDesde"] = r["montoDesde"]!.GetValue<decimal>(),
            ["montoHasta"] = r["montoHasta"]?.GetValue<decimal>(),
            ["activa"] = r["activa"]!.GetValue<bool>(),
            ["pasos"] = r["pasos"]!.DeepClone(),
        })]);
        cambiar?.Invoke(reglas);
        return new JsonObject { ["version"] = conjunto["version"]!.GetValue<string>(), ["reglas"] = reglas };
    }

    private async Task ConReglasRestauradas(Func<ClientePrueba, JsonNode, Task> prueba)
    {
        var admin = await api.ClienteAsync(Usuarios.Admin);
        var original = await admin.GetJsonAsync("/api/admin/reglas");
        try
        {
            await prueba(admin, original);
        }
        finally
        {
            var actual = await admin.GetJsonAsync("/api/admin/reglas");
            var restaurar = Edicion(original);
            ((JsonObject)restaurar)["version"] = actual["version"]!.GetValue<string>();
            await Esperar(await admin.PutAsync("/api/admin/reglas", restaurar), HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Un_hueco_entre_rangos_se_rechaza_sin_guardar_nada()
    {
        await ConReglasRestauradas(async (admin, original) =>
        {
            var respuesta = await admin.PutAsync("/api/admin/reglas", Edicion(original, r => r[1]!["montoDesde"] = 60_000m));

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
            Assert.Equal("Los montos de 50,000.01 a 59,999.99 no tienen regla (entre 'Hasta $50,000' y '$50,000.01 a $250,000').", await DetalleAsync(respuesta));
            Assert.Equal(original["version"]!.GetValue<string>(), (await admin.GetJsonAsync("/api/admin/reglas"))["version"]!.GetValue<string>());
        });
    }

    [Fact]
    public async Task Guardar_sobre_una_version_vieja_responde_409()
    {
        await ConReglasRestauradas(async (admin, original) =>
        {
            await Esperar(await admin.PutAsync("/api/admin/reglas", Edicion(original, r => r[0]!["nombre"] = "Primera edición")), HttpStatusCode.OK);

            var tarde = await admin.PutAsync("/api/admin/reglas", Edicion(original, r => r[0]!["nombre"] = "Segunda edición"));

            Assert.Equal(HttpStatusCode.Conflict, tarde.StatusCode);
            Assert.Contains("Otra persona modificó las reglas", await DetalleAsync(tarde));
        });
    }

    [Fact]
    public async Task Un_cambio_aplica_a_los_nuevos_envios_y_los_documentos_en_curso_conservan_su_cadena()
    {
        await ConReglasRestauradas(async (admin, original) =>
        {
            var creador = await api.ClienteAsync(Usuarios.Creador);
            var enCurso = await creador.DocumentoListoAsync(5_000m);

            var cambio = Edicion(original, r => r[0]!["pasos"] = new JsonArray(new JsonObject { ["nombre"] = "Director General", ["grupoAd"] = "APR_DirectorGeneral" }));
            var guardado = await admin.PutAsync("/api/admin/reglas", cambio);
            await Esperar(guardado, HttpStatusCode.OK);
            var regla = JsonNode.Parse(await guardado.Content.ReadAsStringAsync())!["reglas"]![0]!;
            Assert.Equal("Adriana Admin", regla["modificadoPor"]!.GetValue<string>());

            var nuevo = await creador.DocumentoListoAsync(5_000m);
            string PasoActual(JsonNode doc) => doc["aprobaciones"]!.AsArray().Single(a => a!["estado"]!.GetValue<string>() == "Pendiente")!["nombre"]!.GetValue<string>();
            Assert.Equal("Gerente de Área", PasoActual(await creador.DocumentoAsync(enCurso)));
            Assert.Equal("Director General", PasoActual(await creador.DocumentoAsync(nuevo)));
        });
    }

    [Fact]
    public async Task Un_grupo_agregado_a_las_reglas_da_el_rol_de_aprobador_de_inmediato()
    {
        var nuevo = new Usuario(@"PRUEBA\compras", "Carmen Compras", "carmen@empresa.test", "APR_Compras");
        api.Directorio.Registrar(nuevo);
        var cliente = await api.ClienteAsync(nuevo);
        Assert.DoesNotContain("Aprobador", (await cliente.GetJsonAsync("/api/me"))["roles"]!.AsArray().Select(r => r!.GetValue<string>()));

        await ConReglasRestauradas(async (admin, original) =>
        {
            var cambio = Edicion(original, r => r[0]!["pasos"]!.AsArray().Add(new JsonObject { ["nombre"] = "Compras", ["grupoAd"] = "APR_Compras" }));
            await Esperar(await admin.PutAsync("/api/admin/reglas", cambio), HttpStatusCode.OK);

            // Los grupos de aprobación se guardan en caché 5 minutos; guardar las reglas la invalida.
            Assert.Contains("Aprobador", (await cliente.GetJsonAsync("/api/me"))["roles"]!.AsArray().Select(r => r!.GetValue<string>()));
        });
    }

    [Fact]
    public async Task Verificar_un_grupo_dice_quienes_recibiran_los_avisos()
    {
        var admin = await api.ClienteAsync(Usuarios.Admin);

        var area = await admin.GetJsonAsync("/api/admin/grupos/miembros?grupo=APR_GerenteArea");
        Assert.True(area["existe"]!.GetValue<bool>());
        Assert.Equal(["gabriela@empresa.test", "gerardo@empresa.test"], area["miembros"]!.AsArray().Select(m => m!["email"]!.GetValue<string>()).Order());

        var inexistente = await admin.GetJsonAsync("/api/admin/grupos/miembros?grupo=APR_NoExiste");
        Assert.False(inexistente["existe"]!.GetValue<bool>());
    }
}
