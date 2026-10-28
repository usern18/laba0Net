using AuthService.Dtos;
using AuthService.Models;
using AuthService.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared;

namespace AuthService.Services;

public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokenService,
    JwtOptions jwtOptions,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<(RegisteredUserResponse? User, bool EmailConflict)> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.EmailExistsAsync(email, cancellationToken))
            return (null, true);

        var role = await users.FindRoleByNameAsync("User", cancellationToken)
            ?? throw new InvalidOperationException("The default User role has not been seeded.");
        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        users.Add(user);

        try
        {
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await users.EmailExistsAsync(email, cancellationToken))
                return (null, true);
            throw;
        }

        logger.LogInformation("Registered user {UserId}", user.Id);
        return (new RegisteredUserResponse(user.Id, user.Email, user.DisplayName), false);
    }

    public async Task<TokenPairResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(email, cancellationToken);
        if (user is null || !user.IsActive ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Failed login attempt for {Email}", email);
            return null;
        }

        return await CreateTokenPairAsync(user, cancellationToken);
    }

    public async Task<TokenPairResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await refreshTokens.BeginTransactionAsync(cancellationToken);
        var tokenHash = tokenService.HashRefreshToken(refreshToken);
        var storedToken = await refreshTokens.FindByHashAsync(tokenHash, cancellationToken);
        if (storedToken is null || storedToken.RevokedAt is not null || storedToken.ExpiresAt <= now)
        {
            logger.LogWarning("Rejected invalid or expired refresh token");
            return null;
        }

        var user = await users.FindByIdWithRolesAsync(storedToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            logger.LogWarning("Rejected refresh token for unavailable user {UserId}", storedToken.UserId);
            return null;
        }

        var pair = CreateTokenPair(user);
        var replacementHash = tokenService.HashRefreshToken(pair.RefreshToken);
        storedToken.RevokedAt = now;
        storedToken.ReplacedByToken = replacementHash;
        refreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = replacementHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwtOptions.RefreshTokenDays)
        });

        try
        {
            await refreshTokens.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Rejected concurrently reused refresh token");
            return null;
        }

        return pair;
    }

    public async Task LogoutAsync(int userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashRefreshToken(refreshToken);
        var storedToken = await refreshTokens.FindByHashAndUserAsync(tokenHash, userId, cancellationToken);
        if (storedToken is null || storedToken.RevokedAt is not null)
            return;

        storedToken.RevokedAt = DateTime.UtcNow;
        await refreshTokens.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Revoked refresh token for user {UserId}", userId);
    }

    private async Task<TokenPairResponse> CreateTokenPairAsync(User user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var pair = CreateTokenPair(user);
        refreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = tokenService.HashRefreshToken(pair.RefreshToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwtOptions.RefreshTokenDays)
        });
        await refreshTokens.SaveChangesAsync(cancellationToken);
        return pair;
    }

    private TokenPairResponse CreateTokenPair(User user)
    {
        var pair = tokenService.CreateTokenPair(user, user.UserRoles.Select(userRole => userRole.Role.Name));
        return new TokenPairResponse(pair.AccessToken, pair.RefreshToken, pair.ExpiresAt, "Bearer");
    }
}
