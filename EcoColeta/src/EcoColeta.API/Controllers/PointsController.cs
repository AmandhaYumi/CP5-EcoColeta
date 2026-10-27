using EcoColeta.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EcoColeta.API.Controllers;
[ApiController, Route("api/pontos")]
public sealed class PontosController(PontoColetaService service) : EcoColeta.API.ApiController
{
    [HttpGet]
    public async Task<IActionResult> ObterTodos([FromQuery] PageQuery page, CancellationToken ct) => Paged(await service.ObterTodosAsync(page, ct), "/api/pontos", Resource);
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var r = await service.ObterAsync(id, ct);
        return r.IsSuccess ? Ok(OperationResult<Resource<PontoDto>>.Success(Resource(r.Data!))) : Respond(r);
    }
    [Authorize(Roles = "Admin"), HttpPost]
    public async Task<IActionResult> Adicionar(SolicitarCriacaoPonto request, CancellationToken ct)
    {
        var r = await service.AdicionarAsync(request, ct);
        return r.IsSuccess ? Created($"/api/pontos/{r.Data!.Id}", OperationResult<Resource<PontoDto>>.Success(Resource(r.Data!))) : Respond(r);
    }
    [Authorize(Roles = "Admin"), HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deletar(Guid id, CancellationToken ct) => Respond(await service.DeletarAsync(id, ct));
    private Resource<PontoDto> Resource(PontoDto point)
    {
        var links = new List<Link> { L("self", $"/api/pontos/{point.Id}"), L("collection", "/api/pontos") };
        if (IsAdmin) links.Add(L("delete", $"/api/pontos/{point.Id}", "DELETE"));
        if (User.Identity?.IsAuthenticated == true) links.Add(L("agendar-coleta", "/api/coletas", "POST"));
        return new(point, links);
    }
}
