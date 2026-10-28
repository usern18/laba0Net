using AuthService.Models;

namespace AuthService.Repositories;

public interface IUserRepository : IBaseRepository<User>
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> FindByIdWithRolesAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> FindWithRolesAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<User> Users, int TotalCount)> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Role?> FindRoleByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> FindRolesByNamesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken = default);
    Task<User?> FindWithUserRolesAsync(int id, CancellationToken cancellationToken = default);
}
