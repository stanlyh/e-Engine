using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Infrastructure.Correo;
using Aprobaciones.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aprobaciones.Api.Tests.Infra;

/// <summary>
/// La API completa en memoria contra una base de LocalDB propia (se crea con las migraciones y se borra al final).
/// Solo se sustituyen la autenticación de Windows, Active Directory y el envío de correo.
/// LocalDB guarda sus archivos en el perfil del usuario (C:), así que no depende del disco de la instancia de desarrollo.
/// </summary>
public sealed class AprobacionesFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string RfcEmpresa = "EKU9003173C9";

    private readonly string _nombreBd = $"AprobacionesPruebas_{Guid.NewGuid():N}";

    public string RutaArchivos { get; } = Path.Combine(Path.GetTempPath(), "aprobaciones-integracion", Guid.NewGuid().ToString("N"));
    public DirectorioPrueba Directorio { get; } = new();
    public EnviadorPrueba Correo { get; } = new();

    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_nombreBd};Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting llega como argumento de línea de comandos: tiene prioridad sobre appsettings y ya está
        // disponible cuando Program.cs lee la configuración (antes de Build).
        builder.UseEnvironment("Pruebas");
        builder.UseSetting("ConnectionStrings:Aprobaciones", CadenaConexion);
        builder.UseSetting("Documentos:RutaArchivos", RutaArchivos);
        builder.UseSetting("Documentos:RfcEmpresa", RfcEmpresa);
        builder.UseSetting("Seguridad:GrupoUsuarios", "");
        builder.UseSetting("Seguridad:GrupoTesoreria", "APR_Tesoreria");
        builder.UseSetting("Seguridad:GrupoAdmin", "APR_Admin");
        // Sin proceso en segundo plano: las pruebas procesan la cola con ProcesarCorreosAsync para ser deterministas.
        builder.UseSetting("Correo:Modo", "Desactivado");
        builder.UseSetting("Notificaciones:UrlBase", "https://aprobaciones.prueba");

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, AutenticacionPrueba>(AutenticacionPrueba.Esquema, null);
            services.AddSingleton<IAuthenticationSchemeProvider, ProveedorEsquemasPrueba>();
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = AutenticacionPrueba.Esquema;
                o.DefaultAuthenticateScheme = AutenticacionPrueba.Esquema;
                o.DefaultChallengeScheme = AutenticacionPrueba.Esquema;
                o.DefaultForbidScheme = AutenticacionPrueba.Esquema;
            });
            services.RemoveAll<IDirectorioUsuarios>();
            services.AddSingleton<IDirectorioUsuarios>(Directorio);
            services.RemoveAll<IEnviadorCorreo>();
            services.AddSingleton<IEnviadorCorreo>(Correo);
        });
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AprobacionesDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AprobacionesDbContext>().Database.EnsureDeletedAsync();
        await DisposeAsync();
        if (Directory.Exists(RutaArchivos)) Directory.Delete(RutaArchivos, recursive: true);
    }

    /// <summary>Acceso directo a la base para preparar o comprobar datos.</summary>
    public async Task<T> ConBaseAsync<T>(Func<AprobacionesDbContext, Task<T>> accion)
    {
        await using var scope = Services.CreateAsyncScope();
        return await accion(scope.ServiceProvider.GetRequiredService<AprobacionesDbContext>());
    }

    /// <summary>Una pasada del proceso de envío, como la que hace cada pocos segundos en el servidor.</summary>
    public Task ProcesarCorreosAsync() =>
        ActivatorUtilities.CreateInstance<ProcesadorNotificaciones>(Services).ProcesarLoteAsync(CancellationToken.None);

    public Task<ClientePrueba> ClienteAsync(Usuario usuario, bool conXsrf = true) => ClientePrueba.CrearAsync(this, usuario, conXsrf);
}
