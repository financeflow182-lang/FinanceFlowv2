using FinancasApi.Data;
using FinancasApi.DTOs;
using FinancasApi.Models;
using FinancasApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FinancasApi.Controllers;


// Controlador de autenticação, com registro, login, refresh token e logout
[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, JwtService jwt, IMemoryCache cache, ILogger<AuthController> log) : ControllerBase
{
    private const int BcryptWorkFactor = 12;
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    // Hash falso para igualar o tempo de resposta quando o e-mail não existe (evita enumeração por timing)
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("dummy-password", BcryptWorkFactor);

    [HttpPost("register")]
    [EnableRateLimiting("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        if (req.Password.Contains(email, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "A senha não pode conter o e-mail." });

        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "E-mail já cadastrado." });

        var user = new User
        {
            Name = req.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, BcryptWorkFactor)
        };

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // corrida no índice único de e-mail
            return Conflict(new { message = "E-mail já cadastrado." });
        }

        return Ok(await BuildAuthResponse(user));
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var key = $"login-fail:{email}:{HttpContext.Connection.RemoteIpAddress}";

        cache.TryGetValue(key, out int fails);
        if (fails >= MaxFailedLogins)
        {
            Response.Headers.RetryAfter = ((int)LockDuration.TotalSeconds).ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = "Muitas tentativas. Tente novamente mais tarde." });
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Sempre executa o BCrypt, exista o usuário ou não
        var valid = BCrypt.Net.BCrypt.Verify(req.Password, user?.PasswordHash ?? DummyHash) && user != null;

        if (!valid)
        {
            cache.Set(key, fails + 1, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = LockDuration });
            log.LogWarning("Falha de login para {Email} a partir de {Ip}", email, HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "E-mail ou senha incorretos." });
        }

        cache.Remove(key);
        return Ok(await BuildAuthResponse(user!));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req)
    {
        var hash = JwtService.Hash(req.RefreshToken);

        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == hash);

        if (stored == null || stored.ExpiresAt < DateTime.UtcNow)
            return Unauthorized(new { message = "Refresh token inválido ou expirado." });

        // Reuso de token já rotacionado = possível roubo: revoga todas as sessões do usuário
        if (stored.IsRevoked)
        {
            log.LogWarning("Reuso de refresh token detectado para o usuário {UserId}", stored.UserId);
            await db.RefreshTokens
                .Where(t => t.UserId == stored.UserId && !t.IsRevoked)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true));
            return Unauthorized(new { message = "Refresh token inválido ou expirado." });
        }

        // Rotação atômica: só uma requisição consegue revogar o token
        var revoked = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true));

        if (revoked == 0)
            return Unauthorized(new { message = "Refresh token inválido ou expirado." });

        return Ok(await BuildAuthResponse(stored.User));
    }

    [HttpPost("logout")]
    [EnableRateLimiting("refresh")]
    public async Task<IActionResult> Logout(RefreshRequest req)
    {
        var hash = JwtService.Hash(req.RefreshToken);
        await db.RefreshTokens
            .Where(t => t.Token == hash && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true));
        return NoContent();
    }

    private async Task<AuthResponse> BuildAuthResponse(User user)
    {
        // Limpa tokens expirados/revogados antigos do usuário
        var cutoff = DateTime.UtcNow.AddDays(-1);
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && (t.ExpiresAt < cutoff || (t.IsRevoked && t.ExpiresAt < DateTime.UtcNow)))
            .ExecuteDeleteAsync();

        // Gera os tokens
        var (accessToken, expires) = jwt.GenerateAccessToken(user);
        var (refresh, rawRefresh) = jwt.GenerateRefreshToken(user.Id);

        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync();

        return new AuthResponse(
            accessToken,
            rawRefresh,
            expires,
            new UserDto(user.Id, user.Name, user.Email));
    }
}
