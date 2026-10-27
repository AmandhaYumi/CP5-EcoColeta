using System.ComponentModel.DataAnnotations;
using EcoColeta.Domain;
namespace EcoColeta.Application;
public sealed record SolicitarCadastro([Required, EmailAddress, MaxLength(254)] string Email, [Required, MinLength(12), MaxLength(128)] string Password);
public sealed record SolicitarLogin([Required, EmailAddress, MaxLength(254)] string Email, [Required, MaxLength(128)] string Password);
public sealed record SolicitarCriacaoPonto([Required, StringLength(100, MinimumLength = 1)] string Name, [Required, RegularExpression(@"^\d{8}$")] string PostalCode);
public sealed record SolicitarCriacaoColeta(Guid CollectionPointId, TipoMaterial Material, double WeightKg, DateTime ScheduledAt);
public sealed record PontoDto(Guid Id, string Name, string PostalCode, string Street, string City, string State);
public sealed record ColetaDto(Guid Id, Guid CollectionPointId, Guid UserId, TipoMaterial Material, double WeightKg, DateTime ScheduledAt, StatusColeta Status);
public sealed record UsuarioDto(Guid Id, string Email, string Role);
public sealed record TokenDto(string AccessToken, DateTime ExpiresAt, UsuarioDto User);
public sealed record AddressDto(string PostalCode, string Street, string City, string State);
public interface IPontoRepository
{
    Task<PageResult<PontoDto>> ListAsync(PageQuery page, CancellationToken ct);
    Task<PontoColeta> GetRequiredAsync(Guid id, CancellationToken ct);
    Task<OperationResult<PontoColeta>> AddAsync(PontoColeta point, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
public interface IColetaRepository
{
    Task<PageResult<ColetaDto>> ListAsync(Guid? userId, PageQuery page, CancellationToken ct);
    Task<Coleta> GetRequiredAsync(Guid id, CancellationToken ct);
    Task AddAsync(Coleta pickup, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
public interface IUsuarioRepository
{
    Task<Usuario?> FindAsync(string email, CancellationToken ct);
    Task AddAsync(Usuario user, CancellationToken ct);
}
public interface IAddressGateway { Task<OperationResult<AddressDto>> FindAsync(string postalCode, CancellationToken ct); }
public interface IPasswordService { string Hash(string password); bool Verify(string hash, string password); }
public interface ITokenService { TokenDto Issue(Usuario user); }
public static class Mapping
{
    public static PontoDto Dto(this PontoColeta x) => new(x.Id, x.Name, x.PostalCode, x.Street, x.City, x.State);
    public static ColetaDto Dto(this Coleta x) => new(x.Id, x.CollectionPointId, x.UserId, x.Material, x.WeightKg, x.ScheduledAt, x.Status);
}
