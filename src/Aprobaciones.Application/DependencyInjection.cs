using Aprobaciones.Application.Documentos;
using Microsoft.Extensions.DependencyInjection;

namespace Aprobaciones.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<DocumentosService>();
        return services;
    }
}
