using EcoColeta.Domain;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
namespace EcoColeta.Infrastructure;
public sealed class EcoContext(DbContextOptions<EcoContext> options) : DbContext(options)
{
    public DbSet<PontoColeta> Points => Set<PontoColeta>();
    public DbSet<Coleta> Pickups => Set<Coleta>();
    public DbSet<Usuario> Users => Set<Usuario>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<PontoColeta>().ToTable("Points").HasKey(x => x.Id);
        b.Entity<PontoColeta>().Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Entity<PontoColeta>().HasIndex(x => new { x.PostalCode, x.Name }).IsUnique();
        b.Entity<Usuario>().ToTable("Users").HasKey(x => x.Id);
        b.Entity<Usuario>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Usuario>().Property(x => x.Email).HasMaxLength(254).IsRequired();
        b.Entity<Coleta>().ToTable("Pickups").HasKey(x => x.Id);
        b.Entity<Coleta>().HasOne<PontoColeta>().WithMany().HasForeignKey(x => x.CollectionPointId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Coleta>().HasOne<Usuario>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Coleta>().HasIndex(x => new { x.UserId, x.ScheduledAt, x.Id });
        b.Entity<Coleta>().HasIndex(x => new { x.ScheduledAt, x.Id });
        b.Entity<Coleta>().Property(x => x.Status).IsConcurrencyToken();
        b.Entity<Coleta>().ToTable(t => t.HasCheckConstraint("CK_Pickup_Weight", "\"WeightKg\" > 0 AND \"WeightKg\" <= 1000"));
    }
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await base.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException e) { throw new EcoColeta.Application.RepositoryConflictException("Esta coleta foi alterada por outra pessoa. Consulte os dados novamente.", e); }
        catch (DbUpdateException e) when (e.InnerException is OracleException { Number: 1 or 2290 or 2291 or 2292 })
        { throw new EcoColeta.Application.RepositoryConflictException("Conflito de unicidade ou relacionamento.", e); }
        catch (DbUpdateException e) { throw new EcoColeta.Application.RepositoryUnavailableException(e); }
    }
}
