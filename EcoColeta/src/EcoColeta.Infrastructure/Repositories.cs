using EcoColeta.Application;
using EcoColeta.Domain;
using Microsoft.EntityFrameworkCore;
namespace EcoColeta.Infrastructure;
public sealed class PontoRepository(EcoContext db) : IPontoRepository
{
    public async Task<PageResult<PontoDto>> ListAsync(PageQuery p, CancellationToken ct)
    {
        var q = db.Points.AsNoTracking();
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.Id).Skip((p.PageNumber - 1) * p.PageSize).Take(p.PageSize)
            .Select(x => new PontoDto(x.Id, x.Name, x.PostalCode, x.Street, x.City, x.State)).ToListAsync(ct);
        return new(items, p.PageNumber, p.PageSize, total);
    }
    public async Task<PontoColeta> GetRequiredAsync(Guid id, CancellationToken ct) =>
        await db.Points.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new RepositoryNotFoundException("Ponto de coleta");
    public async Task<OperationResult<PontoColeta>> AddAsync(PontoColeta point, CancellationToken ct)
    {
        if (await db.Points.CountAsync(x => x.Name == point.Name && x.PostalCode == point.PostalCode, ct) > 0)
            return OperationResult<PontoColeta>.Failure(new("point.duplicate", "Já existe um ponto com esse nome e CEP.", ErrorKind.Conflict));
        db.Points.Add(point);
        await db.SaveChangesAsync(ct); // índice único também protege contra corridas
        return OperationResult<PontoColeta>.Success(point);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var point = await GetRequiredAsync(id, ct);
        if (await db.Pickups.CountAsync(x => x.CollectionPointId == id, ct) > 0)
            throw new RepositoryConflictException("Este ponto tem coletas registradas e não pode ser excluído.");
        db.Points.Remove(point); await db.SaveChangesAsync(ct);
    }
}
public sealed class ColetaRepository(EcoContext db) : IColetaRepository
{
    public async Task<PageResult<ColetaDto>> ListAsync(Guid? userId, PageQuery p, CancellationToken ct)
    {
        var q = db.Pickups.AsNoTracking();
        if (userId.HasValue) q = q.Where(x => x.UserId == userId.Value);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.ScheduledAt).ThenBy(x => x.Id).Skip((p.PageNumber - 1) * p.PageSize).Take(p.PageSize)
            .Select(x => new ColetaDto(x.Id, x.CollectionPointId, x.UserId, x.Material, x.WeightKg, x.ScheduledAt, x.Status)).ToListAsync(ct);
        return new(items, p.PageNumber, p.PageSize, total);
    }
    public async Task<Coleta> GetRequiredAsync(Guid id, CancellationToken ct) =>
        await db.Pickups.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new RepositoryNotFoundException("Coleta");
    public async Task AddAsync(Coleta pickup, CancellationToken ct) { db.Pickups.Add(pickup); await db.SaveChangesAsync(ct); }
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
public sealed class UsuarioRepository(EcoContext db) : IUsuarioRepository
{
    public Task<Usuario?> FindAsync(string email, CancellationToken ct)
    { var normalized = email.Trim().ToLowerInvariant(); return db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == normalized, ct); }
    public async Task AddAsync(Usuario user, CancellationToken ct) { db.Users.Add(user); await db.SaveChangesAsync(ct); }
}
