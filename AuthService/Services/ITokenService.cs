using AuthService.Models;

namespace AuthService.Services;

public interface ITokenService
{
    TokenPair CreateTokenPair(User user, IEnumerable<string> roles);
    string HashRefreshToken(string refreshToken);
}

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTime ExpiresAt);
