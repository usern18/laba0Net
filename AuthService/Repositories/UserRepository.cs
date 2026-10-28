using AuthService.Data;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

public sealed class UserRepository(AuthDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Users.FindAsync([id], cancellationToken).AsTask();

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users.Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<User?> FindByIdWithRolesAsync(int id, CancellationToken cancellationToken = default) =>
        db.Users.Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> FindWithRolesAsync(int id, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> FindWithUserRolesAsync(int id, CancellationToken cancellationToken = default) =>
        db.Users.Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public async Task<(IReadOnlyList<User> Users, int TotalCount)> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.Users.AsNoTracking().Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role);
        var count = await query.CountAsync(cancellationToken);
        var users = await query.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (users, count);
    }

    public Task<Role?> FindRoleByNameAsync(string name, CancellationToken cancellationToken = default) =>
        db.Roles.SingleOrDefaultAsync(role => role.Name == name, cancellationToken);

    public async Task<IReadOnlyList<Role>> FindRolesByNamesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken = default) =>
        await db.Roles.Where(role => names.Contains(role.Name)).ToListAsync(cancellationToken);

    public void Add(User entity) => db.Users.Add(entity);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
