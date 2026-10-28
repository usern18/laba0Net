using AuthService.Dtos;
using AuthService.Repositories;

namespace AuthService.Services;

public sealed class UserService(IUserRepository users, ILogger<UserService> logger) : IUserService
{
    public async Task<UserDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await users.FindWithRolesAsync(userId, cancellationToken);
        return user is null ? null : new UserDto(user.Id, user.Email, user.DisplayName, user.CreatedAt, user.IsActive,
            user.UserRoles.Select(userRole => userRole.Role.Name).ToArray());
    }

    public async Task<PagedUsersResponse> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (results, totalCount) = await users.GetPageAsync(page, pageSize, cancellationToken);
        var items = results.Select(user => new UserDto(user.Id, user.Email, user.DisplayName, user.CreatedAt, user.IsActive,
            user.UserRoles.Select(userRole => userRole.Role.Name).ToArray())).ToArray();
        return new PagedUsersResponse(items, page, pageSize, totalCount);
    }

    public async Task<bool> UpdateRolesAsync(int userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
    {
        var allowedNames = new[] { "User", "Admin" };
        if (roleNames.Count == 0 || roleNames.Any(string.IsNullOrWhiteSpace) ||
            roleNames.Any(name => !allowedNames.Contains(name, StringComparer.OrdinalIgnoreCase)))
            return false;
        var normalizedNames = roleNames.Select(name => allowedNames.Single(allowed =>
            string.Equals(allowed, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.Ordinal).ToArray();

        var user = await users.FindWithUserRolesAsync(userId, cancellationToken);
        if (user is null)
            return false;

        var roles = await users.FindRolesByNamesAsync(normalizedNames, cancellationToken);
        if (roles.Count != normalizedNames.Length)
            return false;

        user.UserRoles.Clear();
        foreach (var role in roles)
            user.UserRoles.Add(new Models.UserRole { UserId = user.Id, RoleId = role.Id });

        await users.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated roles for user {UserId}", userId);
        return true;
    }
}
