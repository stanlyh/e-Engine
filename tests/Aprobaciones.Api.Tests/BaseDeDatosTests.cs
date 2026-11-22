using System.Net;
using Aprobaciones.Api.Tests.Infra;
using Microsoft.EntityFrameworkCore;

namespace Aprobaciones.Api.Tests;

public class BaseDeDatosTests(AprobacionesFactory api) : IClassFixture<AprobacionesFactory>
{
    [Fact]
    public async Task Las_migraciones_crean_la_base_y_el_modelo_no_tiene_cambios_sin_migrar()
    {
        var (pendientes, cambiosSinMigrar, reglas) = await api.ConBaseAsync(async db => (
            (await db.Database.GetPendingMigrationsAsync()).ToList(),
            db.Database.HasPendingModelChanges(),
            await db.ReglasAprobacion.CountAsync()));

        Assert.Empty(pendientes);
        Assert.False(cambiosSinMigrar, "Hay cambios en el modelo sin migración: ejecuta 'dotnet ef migrations add'.");
        Assert.Equal(3, reglas);
    }

    [Fact]
    public async Task Salud_responde_ok_con_la_base_al_dia()
    {
        var cliente = await api.ClienteAsync(Usuarios.Ajeno);

        var salud = await cliente.GetJsonAsync("/api/salud");

        Assert.Equal("ok", salud["estado"]!.GetValue<string>());
        Assert.True(salud["baseDeDatos"]!.GetValue<bool>());
        Assert.Empty(salud["migracionesPendientes"]!.AsArray());
        Assert.Equal("Prueba", salud["correo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Sin_usuario_de_Windows_todo_responde_401()
    {
        var anonimo = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/salud")).StatusCode);
    }
}
