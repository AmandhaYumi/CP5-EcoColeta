using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EcoColeta.Application;
using EcoColeta.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
namespace EcoColeta.Infrastructure;
public sealed record JwtSettings(string Key, string Issuer, string Audience);
public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> hasher = new();
    private readonly object user = new();
    public string Hash(string password) => hasher.HashPassword(user, password);
    public bool Verify(string hash, string password) => hasher.VerifyHashedPassword(user, hash, password) != PasswordVerificationResult.Failed;
}
public sealed class TokenService(JwtSettings settings, TimeProvider time) : ITokenService
{
    public TokenDto Issue(Usuario user)
    {
        var expires = time.GetUtcNow().UtcDateTime.AddMinutes(30);
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(ClaimTypes.Role, user.Role), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            notBefore: time.GetUtcNow().UtcDateTime, expires: expires,
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), expires, new(user.Id, user.Email, user.Role));
    }
}
