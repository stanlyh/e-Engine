using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Infrastructure.Archivos;
using Aprobaciones.Infrastructure.Cfdi;
using Aprobaciones.Infrastructure.Directorio;
using Aprobaciones.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aprobaciones.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Aprobaciones") is { Length: > 0 } cs
            ? cs
            : throw new InvalidOperationException("Falta ConnectionStrings:Aprobaciones.");

        services.AddDbContext<AprobacionesDbContext>(o => o.UseSqlServer(connectionString, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
        services.AddScoped<IAprobacionesDbContext>(sp => sp.GetRequiredService<AprobacionesDbContext>());
        services.AddSingleton<IDirectorioUsuarios, DirectorioActiveDirectory>();
        services.AddSingleton<IAlmacenArchivos, AlmacenDisco>();
        services.AddSingleton<ILectorCfdi, LectorCfdi>();
        return services;
    }
}
