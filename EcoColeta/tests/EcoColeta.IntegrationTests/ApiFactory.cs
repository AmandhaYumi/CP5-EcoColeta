using System.Collections.Concurrent;
using System.Net;
using EcoColeta.Application;
using EcoColeta.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
namespace EcoColeta.IntegrationTests;
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public CepHandler External { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:Key", "Integration-only-signing-key-at-least-32-bytes-2026");
        var oracleConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Oracle");
        if (!string.IsNullOrWhiteSpace(oracleConnection))
            builder.UseSetting("ConnectionStrings:Oracle", oracleConnection);
        builder.ConfigureServices(services =>
            services.AddHttpClient<IAddressGateway, ViaCepGateway>().ConfigurePrimaryHttpMessageHandler(() => External));
    }
}
public sealed class CepHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, int> counts = new();
    public int Count(string cep) => counts.GetValueOrDefault(cep);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var cep = request.RequestUri!.Segments[^2].Trim('/');
        var count = counts.AddOrUpdate(cep, 1, (_, n) => n + 1);
        if (cep == "88888888" || (cep == "77777777" && count < 3)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        if (cep == "55555555") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        var json = cep switch
        {
            "99999999" => "{\"erro\":true}",
            "66666666" => "{invalid-json",
            _ => "{\"logradouro\":\"Praça da Sé\",\"localidade\":\"São Paulo\",\"uf\":\"SP\"}"
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
    }
}
