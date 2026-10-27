using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using EcoColeta.API;
using EcoColeta.Application;
using EcoColeta.Domain;
using EcoColeta.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Oracle.ManagedDataAccess.Client;

var builder = WebApplication.CreateBuilder(args);
var key = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(key) && builder.Environment.IsDevelopment()) key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
    throw new InvalidOperationException("Configure Jwt__Key com ao menos 32 bytes (obrigatório fora de Development).");
var jwt = new JwtSettings(key, "EcoColeta", "EcoColeta.Client");
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
var oracleConnection = builder.Configuration.GetConnectionString("Oracle");
if (string.IsNullOrWhiteSpace(oracleConnection))
    throw new InvalidOperationException("Configure ConnectionStrings__Oracle com a conexão do banco Oracle da aplicação.");
builder.Services.AddDbContext<EcoContext>(o => o.UseOracle(oracleConnection));
builder.Services.AddScoped<IPontoRepository, PontoRepository>();
builder.Services.AddScoped<IColetaRepository, ColetaRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<PontoColetaService>(); builder.Services.AddScoped<ColetaService>(); builder.Services.AddScoped<AuthService>();
builder.Services.AddHttpClient<IAddressGateway, ViaCepGateway>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["ViaCep:BaseUrl"] ?? "https://viacep.com.br/ws/");
    c.Timeout = Timeout.InfiniteTimeSpan;
}).AddStandardResilienceHandler(o =>
{
    o.Retry.MaxRetryAttempts = 2;
    o.Retry.Delay = TimeSpan.FromMilliseconds(200);
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    o.CircuitBreaker.MinimumThroughput = 10;
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new()
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ClockSkew = TimeSpan.FromSeconds(15), RoleClaimType = System.Security.Claims.ClaimTypes.Role, NameClaimType = "sub",
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], RequireExpirationTime = true
    };
    o.Events = new JwtBearerEvents
    {
        OnChallenge = async c =>
        { c.HandleResponse(); c.Response.StatusCode = 401; c.Response.Headers.WWWAuthenticate = "Bearer";
          await c.Response.WriteAsJsonAsync(OperationResult<object>.Failure(new("auth.required", "Token ausente, inválido ou expirado.", ErrorKind.Unauthorized))); },
        OnForbidden = async c =>
        { c.Response.StatusCode = 403; await c.Response.WriteAsJsonAsync(OperationResult<object>.Failure(new("access.forbidden", "Seu perfil não tem permissão para realizar esta ação.", ErrorKind.Forbidden))); }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = c =>
    new BadRequestObjectResult(OperationResult<object>.Failure(new("request.invalid", string.Join(" ", c.ModelState.Values.SelectMany(v => v.Errors).Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Corpo inválido." : e.ErrorMessage)), ErrorKind.Validation))));
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("auth", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new()
    { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = async (c, ct) =>
    { c.HttpContext.Response.StatusCode = 429; await c.HttpContext.Response.WriteAsJsonAsync(OperationResult<object>.Failure(new("rate_limit.exceeded", "Muitas tentativas. Aguarde um minuto.", ErrorKind.Unavailable)), ct); };
});
builder.Services.AddResponseCompression();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "EcoColeta CP5", Version = "v1", Description = "Pontos de coleta e agendamentos de reciclagem." });
    o.AddSecurityDefinition("Bearer", new() { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    o.AddSecurityRequirement(new() { [new() { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>() });
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { context.Response.StatusCode = 499; }
    catch (Exception e)
    {
        app.Logger.LogError(e, "Erro na requisição {TraceId}", context.TraceIdentifier);
        if (context.Response.HasStarted) throw;
        var unavailable = e is RepositoryUnavailableException or OracleException;
        context.Response.Clear(); context.Response.StatusCode = unavailable ? 503 : 500;
        await context.Response.WriteAsJsonAsync(OperationResult<object>.Failure(new(unavailable ? "storage.unavailable" : "internal.error", $"Falha ao processar requisição. Referência: {context.TraceIdentifier}", unavailable ? ErrorKind.Unavailable : ErrorKind.Internal)));
    }
});
app.UseResponseCompression();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseRouting(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", async (EcoContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EcoContext>();
    await db.Database.MigrateAsync();
    var adminEmail = builder.Configuration["Seed:AdminEmail"];
    var adminPassword = builder.Configuration["Seed:AdminPassword"];
    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        if (adminPassword.Length < 12) throw new InvalidOperationException("Senha inicial deve ter ao menos 12 caracteres.");
        var normalized = adminEmail.Trim().ToLowerInvariant();
        if (await db.Users.CountAsync(x => x.Email == normalized) == 0)
        { db.Users.Add(new Usuario(normalized, scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(adminPassword), true)); await db.SaveChangesAsync(); }
    }
}
app.Run();
public partial class Program { }
