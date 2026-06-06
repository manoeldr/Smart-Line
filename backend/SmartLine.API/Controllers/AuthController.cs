using Microsoft.AspNetCore.Mvc;
using SmartLine.Core.Interfaces;

namespace SmartLine.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var resultado = await _authService.LoginAsync(request.Login, request.Senha);
        if (resultado is null)
            return Unauthorized(new { mensagem = "Login ou senha inválidos" });

        return Ok(resultado);
    }
}

public record LoginRequest(string Login, string Senha);
