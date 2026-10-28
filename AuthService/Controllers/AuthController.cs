using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthService.Dtos;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;

/// <summary>Issues and rotates authentication tokens.</summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Registers a user with the default User role.</summary>
    [HttpPost("register")]
    [ProducesResponseType<RegisteredUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var (user, emailConflict) = await authService.RegisterAsync(request, cancellationToken);
        if (emailConflict)
            return Conflict(new ProblemDetails { Title = "Email already registered", Status = StatusCodes.Status409Conflict });
        return StatusCode(StatusCodes.Status201Created, user);
    }

    /// <summary>Authenticates a user and returns access and refresh tokens.</summary>
    [HttpPost("login")]
    [ProducesResponseType<TokenPairResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var pair = await authService.LoginAsync(request, cancellationToken);
        return pair is null
            ? Unauthorized(new ProblemDetails { Title = "Invalid email or password", Status = StatusCodes.Status401Unauthorized })
            : Ok(pair);
    }

    /// <summary>Rotates a valid refresh token and returns a new token pair.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<TokenPairResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var pair = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
        return pair is null
            ? Unauthorized(new ProblemDetails { Title = "Invalid or expired refresh token", Status = StatusCodes.Status401Unauthorized })
            : Ok(pair);
    }

    /// <summary>Revokes the current user's refresh token.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(userIdValue, out var userId))
            return Unauthorized();
        await authService.LogoutAsync(userId, request.RefreshToken, cancellationToken);
        return NoContent();
    }
}
