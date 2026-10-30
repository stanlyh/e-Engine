using Aprobaciones.Application.Abstracciones;
using Aprobaciones.Domain.Documentos;
using Aprobaciones.Domain.Reglas;
using Aprobaciones.Domain.Usuarios;
using Aprobaciones.Infrastructure.Persistencia.Configuraciones;
using Microsoft.EntityFrameworkCore;

namespace Aprobaciones.Infrastructure.Persistencia;

public sealed class AprobacionesDbContext(DbContextOptions<AprobacionesDbContext> options)
    : DbContext(options), IAprobacionesDbContext
{
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<DocumentoAprobacion> DocumentoAprobaciones => Set<DocumentoAprobacion>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<ReglaAprobacion> ReglasAprobacion => Set<ReglaAprobacion>();
    public DbSet<ReglaPaso> ReglaPasos => Set<ReglaPaso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>(DocumentoConfiguracion.SecuenciaNumero).StartsAt(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AprobacionesDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(200);
    }
}
