using Aprobaciones.Domain.Documentos;
using Aprobaciones.Domain.Notificaciones;
using Aprobaciones.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aprobaciones.Infrastructure.Persistencia.Configuraciones;

internal sealed class NotificacionConfiguracion : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> b)
    {
        b.ToTable("Notificaciones", t => t.HasCheckConstraint(
            "CK_Notificaciones_Destino", "([UsuarioId] IS NOT NULL AND [GrupoAd] IS NULL) OR ([UsuarioId] IS NULL AND [GrupoAd] IS NOT NULL)"));
        b.HasKey(n => n.Id);

        b.Property(n => n.Asunto).HasMaxLength(300);
        b.Property(n => n.CuerpoHtml).HasColumnType("nvarchar(max)");
        b.Property(n => n.CuerpoTexto).HasColumnType("nvarchar(max)");
        b.Property(n => n.GrupoAd).HasMaxLength(256);
        b.Property(n => n.ExcluirCuentas).HasMaxLength(2000);
        b.Property(n => n.Destinatarios).HasMaxLength(2000);
        b.Property(n => n.UltimoError).HasMaxLength(1000);
        b.Ignore(n => n.CuentasExcluidas);

        b.HasOne<Documento>().WithMany().HasForeignKey(n => n.DocumentoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Usuario>().WithMany().HasForeignKey(n => n.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        // Lo que consulta el proceso de envío en cada ciclo.
        b.HasIndex(n => new { n.Estado, n.ProximoIntento });
    }
}
