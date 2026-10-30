using Aprobaciones.Domain.Documentos;
using Aprobaciones.Domain.Reglas;
using Aprobaciones.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace Aprobaciones.Application.Abstracciones;

public interface IAprobacionesDbContext
{
    DbSet<Documento> Documentos { get; }
    DbSet<DocumentoAprobacion> DocumentoAprobaciones { get; }
    DbSet<Usuario> Usuarios { get; }
    DbSet<ReglaAprobacion> ReglasAprobacion { get; }
    DbSet<ReglaPaso> ReglaPasos { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
