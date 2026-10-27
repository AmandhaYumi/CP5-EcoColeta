using EcoColeta.Application;
using EcoColeta.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EcoColeta.API.Controllers;
[ApiController, Authorize, Route("api/coletas")]
public sealed class ColetasController(ColetaService service) : EcoColeta.API.ApiController
{
    [HttpGet]
    public async Task<IActionResult> ObterTodos([FromQuery] PageQuery page, CancellationToken ct) => Paged(await service.ObterTodosAsync(UserId, IsAdmin, page, ct), "/api/coletas", Resource);
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct) => Single(await service.ObterAsync(id, UserId, IsAdmin, ct));
    [HttpPost]
    public async Task<IActionResult> Adicionar(SolicitarCriacaoColeta request, CancellationToken ct)
    {
        var r = await service.AdicionarAsync(request, UserId, ct);
        return r.IsSuccess ? Created($"/api/coletas/{r.Data!.Id}", OperationResult<Resource<ColetaDto>>.Success(Resource(r.Data!))) : Respond(r);
    }
    [HttpPatch("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct) => Single(await service.EditarAsync(id, UserId, IsAdmin, false, ct));
    [Authorize(Roles = "Admin"), HttpPatch("{id:guid}/concluir")]
    public async Task<IActionResult> Concluir(Guid id, CancellationToken ct) => Single(await service.EditarAsync(id, UserId, IsAdmin, true, ct));
    private IActionResult Single(OperationResult<ColetaDto> r) => r.IsSuccess ? Ok(OperationResult<Resource<ColetaDto>>.Success(Resource(r.Data!))) : Respond(r);
    private Resource<ColetaDto> Resource(ColetaDto pickup)
    {
        var links = new List<Link> { L("self", $"/api/coletas/{pickup.Id}"), L("collection", "/api/coletas"), L("point", $"/api/pontos/{pickup.CollectionPointId}") };
        if (pickup.Status == StatusColeta.Agendada)
        {
            links.Add(L("cancelar", $"/api/coletas/{pickup.Id}/cancelar", "PATCH"));
            if (IsAdmin) links.Add(L("concluir", $"/api/coletas/{pickup.Id}/concluir", "PATCH"));
        }
        return new(pickup, links);
    }
}
