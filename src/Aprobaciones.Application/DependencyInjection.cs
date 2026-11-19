using Aprobaciones.Application.Administracion;
using Aprobaciones.Application.Documentos;
using Aprobaciones.Application.Notificaciones;
using Microsoft.Extensions.DependencyInjection;

namespace Aprobaciones.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<DocumentosService>();
        services.AddScoped<BandejasService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<Notificador>();
        services.AddScoped<ReglasService>();
        services.AddScoped<NotificacionesService>();
        return services;
    }
}
