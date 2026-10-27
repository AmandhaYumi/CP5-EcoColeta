using EcoColeta.Domain;
namespace EcoColeta.Application;
public sealed class ColetaService(IColetaRepository repository, IPontoRepository points, TimeProvider time)
{
    public Task<OperationResult<PageResult<ColetaDto>>> ObterTodosAsync(Guid userId, bool admin, PageQuery page, CancellationToken ct) =>
        ServiceExecution.Run(async () => OperationResult<PageResult<ColetaDto>>.Success(await repository.ListAsync(admin ? null : userId, page, ct)));
    public Task<OperationResult<ColetaDto>> ObterAsync(Guid id, Guid userId, bool admin, CancellationToken ct) => ServiceExecution.Run(async () =>
    {
        var coleta = await repository.GetRequiredAsync(id, ct);
        return !admin && coleta.UserId != userId ? Denied() : OperationResult<ColetaDto>.Success(coleta.Dto());
    });
    public Task<OperationResult<ColetaDto>> AdicionarAsync(SolicitarCriacaoColeta request, Guid userId, CancellationToken ct) => ServiceExecution.Run(async () =>
    {
        await points.GetRequiredAsync(request.CollectionPointId, ct);
        var coleta = new Coleta(request.CollectionPointId, userId, request.Material, request.WeightKg, request.ScheduledAt, time.GetUtcNow().UtcDateTime);
        await repository.AddAsync(coleta, ct);
        return OperationResult<ColetaDto>.Success(coleta.Dto());
    });
    public Task<OperationResult<ColetaDto>> EditarAsync(Guid id, Guid userId, bool admin, bool complete, CancellationToken ct) => ServiceExecution.Run(async () =>
    {
        var coleta = await repository.GetRequiredAsync(id, ct);
        if ((!admin && coleta.UserId != userId) || (complete && !admin)) return Denied();
        if (complete) coleta.Concluir(); else coleta.Cancelar();
        await repository.SaveAsync(ct);
        return OperationResult<ColetaDto>.Success(coleta.Dto());
    });
    private static OperationResult<ColetaDto> Denied() => OperationResult<ColetaDto>.Failure(new("access.forbidden", "Você não tem permissão para acessar esta coleta.", ErrorKind.Forbidden));
}
