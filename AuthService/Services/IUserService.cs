using AuthService.Dtos;

namespace AuthService.Services;

public interface IUserService
{
    Task<UserDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<PagedUsersResponse> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<bool> UpdateRolesAsync(int userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
}
