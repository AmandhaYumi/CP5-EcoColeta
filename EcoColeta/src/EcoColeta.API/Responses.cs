using System.Security.Claims;
using EcoColeta.Application;
using Microsoft.AspNetCore.Mvc;
namespace EcoColeta.API;
public sealed record Link(string Rel, string Href, string Method);
public sealed record Resource<T>(T Data, IReadOnlyList<Link> Links);
public abstract class ApiController : ControllerBase
{
    protected Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);
    protected bool IsAdmin => User.IsInRole("Admin");
    protected IActionResult Respond<T>(OperationResult<T> result, int successStatus = 200) =>
        StatusCode(result.IsSuccess ? successStatus : Status(result.Error!.Kind), result);
    public static int Status(ErrorKind kind) => kind switch
    { ErrorKind.Validation => 400, ErrorKind.NotFound => 404, ErrorKind.Conflict => 409,
      ErrorKind.Unauthorized => 401, ErrorKind.Forbidden => 403, ErrorKind.Unavailable => 503, _ => 500 };
    protected static Link L(string rel, string href, string method = "GET") => new(rel, href, method);
    protected IActionResult Paged<T>(OperationResult<PageResult<T>> result, string path, Func<T, Resource<T>> map)
    {
        if (!result.IsSuccess) return Respond(result);
        var p = result.Data!;
        var links = new List<Link> { L("self", $"{path}?pageNumber={p.PageNumber}&pageSize={p.PageSize}"), L("first", $"{path}?pageNumber=1&pageSize={p.PageSize}") };
        if (p.TotalPages > 0) links.Add(L("last", $"{path}?pageNumber={p.TotalPages}&pageSize={p.PageSize}"));
        if (p.HasNext) links.Add(L("next", $"{path}?pageNumber={p.PageNumber + 1}&pageSize={p.PageSize}"));
        if (p.HasPrevious) links.Add(L("previous", $"{path}?pageNumber={p.PageNumber - 1}&pageSize={p.PageSize}"));
        var page = new PageResult<Resource<T>>(p.Items.Select(map).ToList(), p.PageNumber, p.PageSize, p.TotalRecords);
        return Ok(OperationResult<Resource<PageResult<Resource<T>>>>.Success(new(page, links)));
    }
}
