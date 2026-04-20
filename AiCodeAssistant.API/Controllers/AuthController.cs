using AiCodeAssistant.API.Auth;
using AiCodeAssistant.Domain.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiCodeAssistant.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.RegisterAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (AuthException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.LoginAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (AuthException exception)
        {
            return Unauthorized(exception.Message);
        }
    }
}
