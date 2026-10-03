using FinancasApi.Data;
using FinancasApi.DTOs;
using FinancasApi.Models;
using FinancasApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Controllers;


// Controlador de autenticação, com registro, login e refresh token
[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, JwtService jwt) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        try
        {
            if (await db.Users.AnyAsync(u => u.Email == req.Email))
                return Conflict(new { message = "E-mail já cadastrado." });

            var user = new User
            {
                Name = req.Name,
                Email = req.Email.ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Ok(BuildAuthResponse(user));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Erro ao registrar usuário",
                error = ex.Message,
                details = ex.InnerException?.Message
            });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        try
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant());

            if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Unauthorized(new { message = "E-mail ou senha incorretos." });

            return Ok(BuildAuthResponse(user));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Erro ao realizar login",
                error = ex.Message
            });
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req)
    {
        try
        {
            var stored = await db.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == req.RefreshToken);

            if (stored == null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
                return Unauthorized(new { message = "Refresh token inválido ou expirado." });

            stored.IsRevoked = true;
            await db.SaveChangesAsync();

            return Ok(BuildAuthResponse(stored.User));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Erro no refresh token", error = ex.Message });
        }
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        // Gera os tokens
        var (accessToken, expires) = jwt.GenerateAccessToken(user);
        var refresh = jwt.GenerateRefreshToken(user.Id);

        db.RefreshTokens.Add(refresh);
        db.SaveChanges();

        return new AuthResponse(
            accessToken,
            refresh.Token,
            expires,
            new UserDto(user.Id, user.Name, user.Email));
    }
}