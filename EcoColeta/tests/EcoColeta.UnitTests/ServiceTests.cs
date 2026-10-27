using EcoColeta.Application;
using EcoColeta.Domain;
namespace EcoColeta.UnitTests;
public sealed class ServiceTests
{
    [Fact]
    public async Task Application_translates_repository_not_found_exception()
    {
        var result = await ServiceExecution.Run<string>(() => throw new RepositoryNotFoundException("Ponto"));
        Assert.False(result.IsSuccess); Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }
    [Fact]
    public async Task Application_translates_repository_conflict_exception()
    {
        var result = await ServiceExecution.Run<string>(() => throw new RepositoryConflictException("Histórico existente"));
        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
    }
    [Fact]
    public async Task Application_translates_storage_failure_without_exposing_details()
    {
        var result = await ServiceExecution.Run<string>(() => throw new RepositoryUnavailableException(new Exception("secret")));
        Assert.Equal(ErrorKind.Unavailable, result.Error!.Kind); Assert.DoesNotContain("secret", result.Error.Message);
    }
    [Fact]
    public async Task Application_translates_domain_rule_exception()
    {
        var result = await ServiceExecution.Run<string>(() => throw new ErroRegraDominio("Peso inválido"));
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
    }
    [Fact]
    public async Task Request_cancellation_is_propagated()
    { await Assert.ThrowsAsync<OperationCanceledException>(() => ServiceExecution.Run<string>(() => throw new OperationCanceledException())); }
    [Fact]
    public async Task Duplicate_return_from_repository_is_preserved_by_service()
    {
        var repo = new PointsStub { Duplicate = true };
        var result = await new PontoColetaService(repo, new AddressStub(false)).AdicionarAsync(new("Eco Centro", "01001000"), default);
        Assert.False(result.IsSuccess); Assert.Equal("point.duplicate", result.Error!.Code); Assert.True(repo.AddCalled);
    }
    [Fact]
    public async Task External_failure_prevents_persistence()
    {
        var repo = new PointsStub();
        var result = await new PontoColetaService(repo, new AddressStub(true)).AdicionarAsync(new("Eco Centro", "01001000"), default);
        Assert.Equal(ErrorKind.Unavailable, result.Error!.Kind); Assert.False(repo.AddCalled);
    }
    private sealed class AddressStub(bool fails) : IAddressGateway
    {
        public Task<OperationResult<AddressDto>> FindAsync(string cep, CancellationToken ct) => Task.FromResult(fails
            ? OperationResult<AddressDto>.Failure(new("external.unavailable", "Indisponível", ErrorKind.Unavailable))
            : OperationResult<AddressDto>.Success(new(cep, "Praça da Sé", "São Paulo", "SP")));
    }
    private sealed class PointsStub : IPontoRepository
    {
        public bool Duplicate { get; init; }
        public bool AddCalled { get; private set; }
        public Task<OperationResult<PontoColeta>> AddAsync(PontoColeta point, CancellationToken ct)
        { AddCalled = true; return Task.FromResult(Duplicate ? OperationResult<PontoColeta>.Failure(new("point.duplicate", "Duplicado", ErrorKind.Conflict)) : OperationResult<PontoColeta>.Success(point)); }
        public Task<PontoColeta> GetRequiredAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<PageResult<PontoDto>> ListAsync(PageQuery p, CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    }
}
