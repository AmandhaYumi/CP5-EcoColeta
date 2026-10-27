using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EcoColeta.Infrastructure;
using EcoColeta.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace EcoColeta.IntegrationTests;
public sealed class ApiTests : IDisposable
{
    private readonly ApiFactory factory = new();
    private HttpClient Client() => factory.CreateClient();
    [Fact]
    public async Task Health_and_swagger_are_available()
    {
        using var c = Client(); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }
    [Fact]
    public async Task Anonymous_request_is_blocked_and_tampered_token_is_rejected()
    {
        using var c = Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/coletas")).StatusCode);
        c.DefaultRequestHeaders.Authorization = new("Bearer", "invalid.token.signature");
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/coletas")).StatusCode);
    }
    [Fact]
    public async Task Citizen_cannot_create_point_or_promote_self()
    {
        using var c = await Citizen();
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/pontos", new { name = "Point", postalCode = "01001000" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PatchAsync($"/api/coletas/{Guid.NewGuid()}/concluir", null)).StatusCode);
    }
    [Fact]
    public async Task Registration_login_duplicates_and_bad_credentials()
    {
        using var c = Client();
        var request = new { email = $"person-{Guid.NewGuid():N}@example.org", password = "Citizen!Password2026", role = "Admin" };
        var created = await c.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Citizen", (await Json(created)).GetProperty("data").GetProperty("role").GetString());
        var duplicate = await c.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var login = await c.PostAsJsonAsync("/api/auth/login", new { email = "PERSON@example.org", request.password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var bad = await c.PostAsJsonAsync("/api/auth/login", new { request.email, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal("auth.invalid_credentials", (await Json(bad)).GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/register", new { email = "bad", password = "short" })).StatusCode);
    }
    [Fact]
    public async Task Points_have_pagination_links_external_address_and_repository_duplicate_result()
    {
        using var admin = await Admin();
        var prefix = $"Eco {Guid.NewGuid():N}";
        for (var i = 0; i < 3; i++) await CreatePoint(admin, $"{prefix} {i}");
        var duplicate = await admin.PostAsJsonAsync("/api/pontos", new { name = $"{prefix} 0", postalCode = "01001000" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("point.duplicate", (await Json(duplicate)).GetProperty("error").GetProperty("code").GetString());
        var page = await Json(await admin.GetAsync("/api/pontos?pageNumber=1&pageSize=2"));
        var body = page.GetProperty("data"); var data = body.GetProperty("data");
        var totalRecords = data.GetProperty("totalRecords").GetInt32();
        Assert.True(totalRecords >= 3); Assert.Equal((int)Math.Ceiling(totalRecords / 2d), data.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, data.GetProperty("itemCount").GetInt32());
        Assert.Contains(body.GetProperty("links").EnumerateArray(), l => l.GetProperty("rel").GetString() == "next");
        Assert.Equal("São Paulo", data.GetProperty("items")[0].GetProperty("data").GetProperty("city").GetString());
        var next = body.GetProperty("links").EnumerateArray().Single(l => l.GetProperty("rel").GetString() == "next").GetProperty("href").GetString();
        var nextItemCount = (await Json(await admin.GetAsync(next))).GetProperty("data").GetProperty("data").GetProperty("itemCount").GetInt32();
        Assert.InRange(nextItemCount, 1, 2);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/pontos?pageNumber=0&pageSize=101")).StatusCode);
    }
    [Fact]
    public async Task Missing_point_exception_becomes_404_operation_result()
    {
        using var citizen = await Citizen();
        var response = await citizen.PostAsJsonAsync("/api/coletas", Payload(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await Json(response); Assert.False(body.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("resource.not_found", body.GetProperty("error").GetProperty("code").GetString());
    }
    [Fact]
    public async Task Pickup_lifecycle_enforces_ownership_roles_and_terminal_state()
    {
        using var admin = await Admin(); var point = await CreatePoint(admin);
        using var owner = await Citizen(); using var stranger = await Citizen();
        var created = await owner.PostAsJsonAsync("/api/coletas", Payload(point));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); Assert.NotNull(created.Headers.Location);
        var resource = (await Json(created)).GetProperty("data"); var id = resource.GetProperty("data").GetProperty("id").GetGuid();
        Assert.DoesNotContain(resource.GetProperty("links").EnumerateArray(), l => l.GetProperty("rel").GetString() == "complete");
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/api/coletas/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PatchAsync($"/api/coletas/{id}/cancelar", null)).StatusCode);
        var strangersPage = await Json(await stranger.GetAsync("/api/coletas"));
        Assert.Equal(0, strangersPage.GetProperty("data").GetProperty("data").GetProperty("totalRecords").GetInt32());
        var completed = await admin.PatchAsync($"/api/coletas/{id}/concluir", null);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var completedResource = (await Json(completed)).GetProperty("data");
        Assert.Equal("Concluida", completedResource.GetProperty("data").GetProperty("status").GetString());
        Assert.DoesNotContain(completedResource.GetProperty("links").EnumerateArray(), l => l.GetProperty("rel").GetString() is "cancelar" or "concluir");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsync($"/api/coletas/{id}/cancelar", null)).StatusCode);
        var delete = await admin.DeleteAsync($"/api/pontos/{point}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }
    [Fact]
    public async Task Owner_can_cancel_and_invalid_domain_data_is_rejected()
    {
        using var admin = await Admin(); var point = await CreatePoint(admin); using var owner = await Citizen();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/coletas", new { collectionPointId = point, material = "Vidro", weightKg = -1, scheduledAt = DateTime.UtcNow.AddDays(1) })).StatusCode);
        var created = await owner.PostAsJsonAsync("/api/coletas", Payload(point));
        var id = (await Json(created)).GetProperty("data").GetProperty("data").GetProperty("id").GetGuid();
        var cancelled = await owner.PatchAsync($"/api/coletas/{id}/cancelar", null);
        Assert.Equal("Cancelada", (await Json(cancelled)).GetProperty("data").GetProperty("data").GetProperty("status").GetString());
    }
    [Fact]
    public async Task Empty_point_can_be_deleted_and_then_returns_404()
    {
        using var admin = await Admin(); var id = await CreatePoint(admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/pontos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/pontos/{id}")).StatusCode);
    }
    [Theory]
    [InlineData("99999999", HttpStatusCode.NotFound)]
    [InlineData("88888888", HttpStatusCode.ServiceUnavailable)]
    [InlineData("66666666", HttpStatusCode.ServiceUnavailable)]
    [InlineData("55555555", HttpStatusCode.BadRequest)]
    public async Task External_errors_are_translated_and_do_not_persist(string cep, HttpStatusCode expected)
    {
        using var admin = await Admin();
        using var beforeScope = factory.Services.CreateScope();
        var beforeDb = beforeScope.ServiceProvider.GetRequiredService<EcoContext>();
        var pointsBefore = await beforeDb.Points.CountAsync();
        var response = await admin.PostAsJsonAsync("/api/pontos", new { name = "Failed", postalCode = cep });
        Assert.Equal(expected, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcoContext>();
        Assert.Contains("Oracle", db.Database.ProviderName);
        Assert.Equal(pointsBefore, await db.Points.CountAsync());
        Assert.Equal(cep == "88888888" ? 3 : 1, factory.External.Count(cep));
    }
    [Fact]
    public async Task Transient_external_failure_retries_then_succeeds()
    {
        using var admin = await Admin(); await CreatePoint(admin, $"Resilient {Guid.NewGuid():N}", "77777777");
        Assert.Equal(3, factory.External.Count("77777777"));
    }
    [Fact]
    public async Task Real_database_constraints_and_repository_exception_are_enforced()
    {
        using var admin = await Admin(); var id = await CreatePoint(admin);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<EcoContext>();
        Assert.Contains("InitialCreate", (await db.Database.GetAppliedMigrationsAsync()).Single());
        var repo = scope.ServiceProvider.GetRequiredService<IPontoRepository>();
        await Assert.ThrowsAsync<RepositoryNotFoundException>(() => repo.GetRequiredAsync(Guid.NewGuid(), default));
        var existing = await repo.GetRequiredAsync(id, default);
        var result = await repo.AddAsync(new EcoColeta.Domain.PontoColeta(existing.Name, existing.PostalCode, existing.Street, existing.City, existing.State), default);
        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
        db.Points.Add(new(existing.Name, existing.PostalCode, existing.Street, existing.City, existing.State));
        await Assert.ThrowsAsync<RepositoryConflictException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Optimistic_concurrency_prevents_overwriting_completed_pickup()
    {
        using var admin = await Admin(); var point = await CreatePoint(admin); using var citizen = await Citizen();
        var created = await citizen.PostAsJsonAsync("/api/coletas", Payload(point));
        var id = (await Json(created)).GetProperty("data").GetProperty("data").GetProperty("id").GetGuid();
        using var firstScope = factory.Services.CreateScope(); using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IColetaRepository>();
        var second = secondScope.ServiceProvider.GetRequiredService<IColetaRepository>();
        var a = await first.GetRequiredAsync(id, default); var b = await second.GetRequiredAsync(id, default);
        a.Concluir(); b.Cancelar(); await first.SaveAsync(default);
        await Assert.ThrowsAsync<RepositoryConflictException>(() => second.SaveAsync(default));
        var stored = await Json(await citizen.GetAsync($"/api/coletas/{id}"));
        Assert.Equal("Concluida", stored.GetProperty("data").GetProperty("data").GetProperty("status").GetString());
    }
    [Fact]
    public async Task Expired_or_wrong_issuer_tokens_are_rejected()
    {
        using var citizen = await Citizen(); // initializes the host and creates a valid user
        var settings = factory.Services.GetRequiredService<JwtSettings>();
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(settings.Key));
        var signing = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        foreach (var expired in new[] { true, false })
        {
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(expired ? settings.Issuer : "WrongIssuer", settings.Audience,
                [new("sub", Guid.NewGuid().ToString())], notBefore: DateTime.UtcNow.AddHours(-2),
                expires: expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1), signingCredentials: signing);
            citizen.DefaultRequestHeaders.Authorization = new("Bearer", new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token));
            Assert.Equal(HttpStatusCode.Unauthorized, (await citizen.GetAsync("/api/coletas")).StatusCode);
        }
    }
    [Fact]
    public async Task Authentication_endpoints_reject_excessive_requests()
    {
        using var c = Client();
        for (var i = 0; i < 20; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/login", new { email = "invalid", password = "x" })).StatusCode);
        var limited = await c.PostAsJsonAsync("/api/auth/login", new { email = "invalid", password = "x" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limit.exceeded", (await Json(limited)).GetProperty("error").GetProperty("code").GetString());
    }
    private async Task<HttpClient> Admin() => await Login("admin@ecocoleta.local", "EcoColeta!Demo2026");
    private async Task<HttpClient> Citizen()
    {
        var email = $"citizen-{Guid.NewGuid():N}@example.org";
        using var c = Client(); var response = await c.PostAsJsonAsync("/api/auth/register", new { email, password = "Citizen!Password2026" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return await Login(email, "Citizen!Password2026");
    }
    private async Task<HttpClient> Login(string email, string password)
    {
        var c = Client(); var response = await c.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await Json(response)).GetProperty("data").GetProperty("accessToken").GetString()); return c;
    }
    private static object Payload(Guid point) => new { collectionPointId = point, material = "Vidro", weightKg = 3.5, scheduledAt = DateTime.UtcNow.AddDays(1) };
    private static async Task<Guid> CreatePoint(HttpClient c, string? name = null, string cep = "01001000")
    {
        name ??= $"Eco Centro {Guid.NewGuid():N}";
        var response = await c.PostAsJsonAsync("/api/pontos", new { name, postalCode = cep });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("data").GetProperty("data").GetProperty("id").GetGuid();
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>());
    public void Dispose() => factory.Dispose();
}
