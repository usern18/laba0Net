using AuthService.Dtos;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthService.Controllers;

/// <summary>Provides the current profile and administrative user management.</summary>
[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Returns the profile of the authenticated user.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(value, out var userId))
            return Unauthorized();
        var profile = await userService.GetByIdAsync(userId, cancellationToken);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    /// <summary>Returns a page of user profiles for administrators.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<PagedUsersResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["page/pageSize"] = ["Page must be positive and pageSize must be between 1 and 100."] }));
        return Ok(await userService.GetPageAsync(page, pageSize, cancellationToken));
    }

    /// <summary>Replaces a user's role set.</summary>
    [HttpPut("{id:int}/roles")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRoles(int id, [FromBody] UpdateRolesRequest request, CancellationToken cancellationToken)
    {
        var updated = await userService.UpdateRolesAsync(id, request.Roles, cancellationToken);
        if (updated)
            return NoContent();
        if (await userService.GetByIdAsync(id, cancellationToken) is null)
            return NotFound(new ProblemDetails { Title = "User not found", Status = StatusCodes.Status404NotFound });
        return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["roles"] = ["At least one supported role (User or Admin) is required."] }));
    }
}