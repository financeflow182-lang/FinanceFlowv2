using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FinancasApi.Models;
using Microsoft.IdentityModel.Tokens;

namespace FinancasApi.Services;
//JWT geração dos tokens
public class JwtService(IConfiguration config)
{
    private readonly string _secret    = config["JwtSettings:SecretKey"]!;
    private readonly string _issuer    = config["JwtSettings:Issuer"]!;
    private readonly string _audience  = config["JwtSettings:Audience"]!;
    private readonly int    _expMin    = int.Parse(config["JwtSettings:ExpirationMinutes"]!);
    private readonly int    _refDays   = int.Parse(config["JwtSettings:RefreshExpirationDays"]!);

    public (string token, DateTime expires) GenerateAccessToken(User user)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var exp   = DateTime.UtcNow.AddMinutes(_expMin);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name,  user.Name),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(_issuer, _audience, claims, expires: exp, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), exp);
    }

    // O valor puro só é devolvido ao cliente; no banco fica apenas o hash SHA-256
    public (RefreshToken entity, string raw) GenerateRefreshToken(int userId)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            Token     = Hash(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(_refDays),
            UserId    = userId
        };
        return (entity, raw);
    }

    public static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
