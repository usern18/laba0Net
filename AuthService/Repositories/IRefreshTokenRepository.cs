using AuthService.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuthService.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<RefreshToken?> FindByHashAndUserAsync(string tokenHash, int userId, CancellationToken cancellationToken = default);
    void Add(RefreshToken token);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
