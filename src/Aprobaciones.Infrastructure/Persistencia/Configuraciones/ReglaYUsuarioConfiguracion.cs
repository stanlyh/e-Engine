using Aprobaciones.Domain.Reglas;
using Aprobaciones.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aprobaciones.Infrastructure.Persistencia.Configuraciones;

internal sealed class ReglaAprobacionConfiguracion : IEntityTypeConfiguration<ReglaAprobacion>
{
    public void Configure(EntityTypeBuilder<ReglaAprobacion> b)
    {
        b.ToTable("ReglasAprobacion");
        b.Property(r => r.Nombre).HasMaxLength(100);
        b.HasMany(r => r.Pasos).WithOne().HasForeignKey(p => p.ReglaAprobacionId).OnDelete(DeleteBehavior.Cascade);

        // Rangos iniciales; se editan después desde la pantalla de administración.
        // Los grupos deben existir en AD (formato DOMINIO\Grupo en producción).
        b.HasData(
            new { Id = 1, Nombre = "Hasta $50,000", MontoDesde = 0m, MontoHasta = (decimal?)50_000m, Activa = true },
            new { Id = 2, Nombre = "$50,000.01 a $250,000", MontoDesde = 50_000.01m, MontoHasta = (decimal?)250_000m, Activa = true },
            new { Id = 3, Nombre = "Más de $250,000", MontoDesde = 250_000.01m, MontoHasta = (decimal?)null, Activa = true });
    }
}

internal sealed class ReglaPasoConfiguracion : IEntityTypeConfiguration<ReglaPaso>
{
    private const string GerenteArea = "APR_GerenteArea";
    private const string GerenteFinanciero = "APR_GerenteFinanciero";
    private const string DirectorGeneral = "APR_DirectorGeneral";

    public void Configure(EntityTypeBuilder<ReglaPaso> b)
    {
        b.ToTable("ReglaPasos");
        b.Property(p => p.Nombre).HasMaxLength(100);
        b.Property(p => p.GrupoAd).HasMaxLength(256);
        b.HasIndex(p => new { p.ReglaAprobacionId, p.Orden }).IsUnique();

        b.HasData(
            new { Id = 1, ReglaAprobacionId = 1, Orden = 1, Nombre = "Gerente de Área", GrupoAd = GerenteArea },
            new { Id = 2, ReglaAprobacionId = 2, Orden = 1, Nombre = "Gerente de Área", GrupoAd = GerenteArea },
            new { Id = 3, ReglaAprobacionId = 2, Orden = 2, Nombre = "Gerente Financiero", GrupoAd = GerenteFinanciero },
            new { Id = 4, ReglaAprobacionId = 3, Orden = 1, Nombre = "Gerente de Área", GrupoAd = GerenteArea },
            new { Id = 5, ReglaAprobacionId = 3, Orden = 2, Nombre = "Gerente Financiero", GrupoAd = GerenteFinanciero },
            new { Id = 6, ReglaAprobacionId = 3, Orden = 3, Nombre = "Director General", GrupoAd = DirectorGeneral });
    }
}

internal sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.Property(u => u.Cuenta).HasMaxLength(256);
        b.Property(u => u.Nombre).HasMaxLength(200);
        b.Property(u => u.Email).HasMaxLength(256);
        b.HasIndex(u => u.Cuenta).IsUnique();
    }
}
