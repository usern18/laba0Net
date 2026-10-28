namespace AuthService.Dtos;

/// <summary>Access and rotating refresh credentials.</summary>
public sealed record TokenPairResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, string TokenType);

/// <summary>Identifier and safe public data returned after registration.</summary>
public sealed record RegisteredUserResponse(int Id, string Email, string DisplayName);
