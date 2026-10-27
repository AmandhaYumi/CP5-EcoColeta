using EcoColeta.Domain;
namespace EcoColeta.Application;
public sealed class PontoColetaService(IPontoRepository repository, IAddressGateway addresses)
{
    public Task<OperationResult<PageResult<PontoDto>>> ObterTodosAsync(PageQuery page, CancellationToken ct) =>
        ServiceExecution.Run(async () => OperationResult<PageResult<PontoDto>>.Success(await repository.ListAsync(page, ct)));
    public Task<OperationResult<PontoDto>> ObterAsync(Guid id, CancellationToken ct) =>
        ServiceExecution.Run(async () => OperationResult<PontoDto>.Success((await repository.GetRequiredAsync(id, ct)).Dto()));
    public Task<OperationResult<PontoDto>> AdicionarAsync(SolicitarCriacaoPonto request, CancellationToken ct) => ServiceExecution.Run(async () =>
    {
        var address = await addresses.FindAsync(request.PostalCode, ct);
        if (!address.IsSuccess) return OperationResult<PontoDto>.Failure(address.Error!);
        var a = address.Data!;
        var result = await repository.AddAsync(new PontoColeta(request.Name, a.PostalCode, a.Street, a.City, a.State), ct);
        return result.IsSuccess ? OperationResult<PontoDto>.Success(result.Data!.Dto()) : OperationResult<PontoDto>.Failure(result.Error!);
    });
    public Task<OperationResult<bool>> DeletarAsync(Guid id, CancellationToken ct) => ServiceExecution.Run(async () =>
    { await repository.DeleteAsync(id, ct); return OperationResult<bool>.Success(true); });
}
