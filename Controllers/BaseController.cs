using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinancasApi.Controllers;

// Controlador base para autenticação e obtenção do ID do usuário a partir do token JWT
[Authorize]
[ApiController]
public abstract class BaseController : ControllerBase
{
    protected int UserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")!);
}