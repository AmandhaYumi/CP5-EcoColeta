using EcoColeta.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace EcoColeta.API.Controllers;
[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public sealed class AuthController(AuthService service) : EcoColeta.API.ApiController
{
    [HttpPost("register")]
    public async Task<IActionResult> Adicionar(SolicitarCadastro request, CancellationToken ct) => Respond(await service.AdicionarAsync(request, ct), 201);
    [HttpPost("login")]
    public async Task<IActionResult> Autenticar(SolicitarLogin request, CancellationToken ct) => Respond(await service.AutenticarAsync(request, ct));
}
