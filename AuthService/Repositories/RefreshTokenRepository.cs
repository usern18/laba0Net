using AuthService.Data;
using AuthService.Models;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

public sealed class RefreshTokenRepository(AuthDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.RefreshTokens.SingleOrDefaultAsync(token => token.Token == tokenHash, cancellationToken);

    public Task<RefreshToken?> FindByHashAndUserAsync(string tokenHash, int userId, CancellationToken cancellationToken = default) =>
        db.RefreshTokens.SingleOrDefaultAsync(token => token.Token == tokenHash && token.UserId == userId, cancellationToken);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
    public async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        await db.Database.BeginTransactionAsync(cancellationToken);
}
