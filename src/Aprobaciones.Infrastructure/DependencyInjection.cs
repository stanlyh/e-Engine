using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Infrastructure.Archivos;
using Aprobaciones.Infrastructure.Cfdi;
using Aprobaciones.Infrastructure.Correo;
using Aprobaciones.Infrastructure.Directorio;
using Aprobaciones.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aprobaciones.Infrastructure;

public static class DependencyInjection
{
    /// <param name="esDesarrollo">Habilita el directorio simulado (sección DirectorioSimulado) en lugar de Active Directory.</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool esDesarrollo)
    {
        var connectionString = configuration.GetConnectionString("Aprobaciones") is { Length: > 0 } cs
            ? cs
            : throw new InvalidOperationException("Falta ConnectionStrings:Aprobaciones.");

        services.AddDbContext<AprobacionesDbContext>(o => o.UseSqlServer(connectionString, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        services.AddScoped<IAprobacionesDbContext>(sp => sp.GetRequiredService<AprobacionesDbContext>());
        services.AddSingleton<IAlmacenArchivos, AlmacenDisco>();
        services.AddSingleton<ILectorCfdi, LectorCfdi>();

        if (esDesarrollo && configuration.GetSection(OpcionesDirectorioSimulado.Seccion).Exists())
        {
            services.Configure<OpcionesDirectorioSimulado>(configuration.GetSection(OpcionesDirectorioSimulado.Seccion));
            services.AddSingleton<IDirectorioUsuarios, DirectorioSimulado>();
        }
        else
        {
            services.AddSingleton<IDirectorioUsuarios, DirectorioActiveDirectory>();
        }

        services.AddOptions<OpcionesCorreo>()
            .Bind(configuration.GetSection(OpcionesCorreo.Seccion))
            .Validate(o => o.Modo == ModoCorreo.Desactivado || !string.IsNullOrWhiteSpace(o.Remitente), "Correo:Remitente es obligatorio para enviar correos.")
            .Validate(o => o.Modo != ModoCorreo.Carpeta || (o.Carpeta is { Length: > 0 } c && Path.IsPathRooted(c)), "Correo:Carpeta debe ser una ruta absoluta en modo Carpeta.")
            .Validate(o => o.Modo != ModoCorreo.Smtp || !string.IsNullOrWhiteSpace(o.Host), "Correo:Host es obligatorio en modo Smtp.")
            .ValidateOnStart();
        services.AddSingleton<IEnviadorCorreo, EnviadorSmtp>();
        // Desactivado: los avisos se acumulan como pendientes y se envían al activarlo.
        if (configuration.GetSection(OpcionesCorreo.Seccion).GetValue<ModoCorreo>(nameof(OpcionesCorreo.Modo)) != ModoCorreo.Desactivado)
            services.AddHostedService<ProcesadorNotificaciones>();
        return services;
    }
}
