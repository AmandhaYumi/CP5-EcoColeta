namespace EcoColeta.Application;
public enum ErrorKind { Validation, NotFound, Conflict, Unauthorized, Forbidden, Unavailable, Internal }
public sealed record AppError(string Code, string Message, ErrorKind Kind);
public sealed class OperationResult<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public AppError? Error { get; }
    private OperationResult(bool success, T? data, AppError? error) { IsSuccess = success; Data = data; Error = error; }
    public static OperationResult<T> Success(T data) => new(true, data, null);
    public static OperationResult<T> Failure(AppError error) => new(false, default, error);
}
public sealed record PageResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalRecords)
{
    public int TotalPages => (int)Math.Ceiling(TotalRecords / (double)PageSize);
    public int ItemCount => Items.Count;
    public bool HasPrevious => PageNumber > 1;
    public bool HasNext => PageNumber < TotalPages;
}
public sealed class PageQuery
{
    [System.ComponentModel.DataAnnotations.Range(1, 1000000)] public int PageNumber { get; set; } = 1;
    [System.ComponentModel.DataAnnotations.Range(1, 100)] public int PageSize { get; set; } = 10;
}
public sealed class RepositoryNotFoundException(string resource) : Exception($"{resource} não encontrado.");
public sealed class RepositoryConflictException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class RepositoryUnavailableException(Exception inner) : Exception("Não foi possível acessar o banco de dados.", inner);
public static class ServiceExecution
{
    public static async Task<OperationResult<T>> Run<T>(Func<Task<OperationResult<T>>> action)
    {
        try { return await action(); }
        catch (RepositoryNotFoundException e) { return OperationResult<T>.Failure(new("resource.not_found", e.Message, ErrorKind.NotFound)); }
        catch (RepositoryConflictException e) { return OperationResult<T>.Failure(new("resource.conflict", e.Message, ErrorKind.Conflict)); }
        catch (RepositoryUnavailableException) { return OperationResult<T>.Failure(new("storage.unavailable", "O banco de dados está indisponível no momento.", ErrorKind.Unavailable)); }
        catch (EcoColeta.Domain.ErroRegraDominio e) { return OperationResult<T>.Failure(new("domain.rule", e.Message, ErrorKind.Validation)); }
    }
}
