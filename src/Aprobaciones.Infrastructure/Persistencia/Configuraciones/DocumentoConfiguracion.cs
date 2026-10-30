using Aprobaciones.Domain.Documentos;
using Aprobaciones.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aprobaciones.Infrastructure.Persistencia.Configuraciones;

internal sealed class DocumentoConfiguracion : IEntityTypeConfiguration<Documento>
{
    public const string SecuenciaNumero = "DocumentoNumeros";

    public void Configure(EntityTypeBuilder<Documento> b)
    {
        b.ToTable("Documentos");
        b.HasKey(d => d.Id);

        b.Property(d => d.Numero).HasDefaultValueSql($"NEXT VALUE FOR dbo.{SecuenciaNumero}").ValueGeneratedOnAdd();
        b.Property(d => d.Folio)
            .HasMaxLength(20)
            .HasComputedColumnSql(
                "'DOC-' + IIF([Numero] > 999999, CAST([Numero] AS varchar(10)), RIGHT('00000' + CAST([Numero] AS varchar(10)), 6))",
                stored: true);
        b.HasIndex(d => d.Folio).IsUnique();

        b.Property(d => d.Descripcion).HasMaxLength(500);
        b.Property(d => d.ReferenciaTesoreria).HasMaxLength(100);
        b.Property(d => d.RowVersion).IsRowVersion();

        b.OwnsOne(d => d.Cfdi, c =>
        {
            c.Property(x => x.Uuid).HasColumnName("CfdiUuid");
            c.Property(x => x.RfcEmisor).HasColumnName("CfdiRfcEmisor").HasMaxLength(13);
            c.Property(x => x.NombreEmisor).HasColumnName("CfdiNombreEmisor").HasMaxLength(300);
            c.Property(x => x.RfcReceptor).HasColumnName("CfdiRfcReceptor").HasMaxLength(13);
            c.Property(x => x.Serie).HasColumnName("CfdiSerie").HasMaxLength(25);
            c.Property(x => x.Folio).HasColumnName("CfdiFolio").HasMaxLength(40);
            c.Property(x => x.Fecha).HasColumnName("CfdiFecha");
            c.Property(x => x.SubTotal).HasColumnName("CfdiSubTotal").HasPrecision(18, 6);
            c.Property(x => x.Total).HasColumnName("CfdiTotal").HasPrecision(18, 6);
            c.Property(x => x.Moneda).HasColumnName("CfdiMoneda").HasMaxLength(3);
            c.Property(x => x.TipoCambio).HasColumnName("CfdiTipoCambio").HasPrecision(18, 6);
            c.Property(x => x.TipoComprobante).HasColumnName("CfdiTipoComprobante").HasMaxLength(1);
            c.Ignore(x => x.MontoMxn);
            // Una factura no puede registrarse en dos documentos.
            c.HasIndex(x => x.Uuid).IsUnique().HasFilter("[CfdiUuid] IS NOT NULL");
        });

        b.HasOne<Usuario>().WithMany().HasForeignKey(d => d.CreadoPorId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(d => d.Archivos).WithOne().HasForeignKey(a => a.DocumentoId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(d => d.Aprobaciones).WithOne().HasForeignKey(a => a.DocumentoId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(d => d.Historial).WithOne().HasForeignKey(h => h.DocumentoId).OnDelete(DeleteBehavior.Cascade);

        b.Ignore(d => d.EsEditable);
        b.Ignore(d => d.RondaActual);
        b.Ignore(d => d.PasoActual);

        b.HasIndex(d => new { d.CreadoPorId, d.Estado });
        b.HasIndex(d => d.Estado);
    }
}

internal sealed class DocumentoArchivoConfiguracion : IEntityTypeConfiguration<DocumentoArchivo>
{
    public void Configure(EntityTypeBuilder<DocumentoArchivo> b)
    {
        b.ToTable("DocumentoArchivos");
        b.Property(a => a.NombreOriginal).HasMaxLength(260);
        b.Property(a => a.RutaRelativa).HasMaxLength(400);
        b.Property(a => a.ContentType).HasMaxLength(100);
        b.Property(a => a.HashSha256).HasMaxLength(64).IsFixedLength();
        b.HasOne<Usuario>().WithMany().HasForeignKey(a => a.SubidoPorId).OnDelete(DeleteBehavior.Restrict);
        // Solo un XML y un PDF vigentes por documento; las versiones anteriores se conservan.
        b.HasIndex(a => new { a.DocumentoId, a.Tipo }).IsUnique().HasFilter("[Vigente] = 1");
    }
}

internal sealed class DocumentoAprobacionConfiguracion : IEntityTypeConfiguration<DocumentoAprobacion>
{
    public void Configure(EntityTypeBuilder<DocumentoAprobacion> b)
    {
        b.ToTable("DocumentoAprobaciones");
        b.Property(a => a.Nombre).HasMaxLength(100);
        b.Property(a => a.GrupoAd).HasMaxLength(256);
        b.Property(a => a.Comentario).HasMaxLength(1000);
        b.HasOne<Usuario>().WithMany().HasForeignKey(a => a.ResueltoPorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.DocumentoId, a.Ronda, a.Orden }).IsUnique();
        // Bandeja de aprobadores: pasos pendientes por grupo.
        b.HasIndex(a => new { a.Estado, a.GrupoAd });
    }
}

internal sealed class DocumentoHistorialConfiguracion : IEntityTypeConfiguration<DocumentoHistorial>
{
    public void Configure(EntityTypeBuilder<DocumentoHistorial> b)
    {
        b.ToTable("DocumentoHistorial");
        b.Property(h => h.Comentario).HasMaxLength(1000);
        b.HasOne<Usuario>().WithMany().HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(h => new { h.DocumentoId, h.Fecha });
    }
}
