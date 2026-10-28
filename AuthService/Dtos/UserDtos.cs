using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

/// <summary>Public user profile without credentials or persistence details.</summary>
public sealed record UserDto(int Id, string Email, string DisplayName, DateTime CreatedAt, bool IsActive, IReadOnlyList<string> Roles);

/// <summary>Paginated user list.</summary>
public sealed record PagedUsersResponse(IReadOnlyList<UserDto> Items, int Page, int PageSize, int TotalCount);

/// <summary>Request to replace a user's role set.</summary>
public sealed class UpdateRolesRequest
{
    /// <summary>Roles to assign. Only User and Admin are supported.</summary>
    [Required, MinLength(1)]
    public List<string> Roles { get; set; } = [];
}
