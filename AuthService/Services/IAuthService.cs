using AuthService.Dtos;

namespace AuthService.Services;

public interface IAuthService
{
    Task<(RegisteredUserResponse? User, bool EmailConflict)> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<TokenPairResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<TokenPairResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(int userId, string refreshToken, CancellationToken cancellationToken = default);
}
