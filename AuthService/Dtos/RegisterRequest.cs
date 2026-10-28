using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

/// <summary>Credentials and display name required to register a standard user.</summary>
public class RegisterRequest
{
    /// <summary>Unique email address used to sign in.</summary>
    [Required, EmailAddress]
    public string Email { get; set; } = null!;
    /// <summary>Password of at least six characters; it is stored only as a password hash.</summary>
    [Required, MinLength(6)]
    public string Password { get; set; } = null!;
    /// <summary>Name shown in the user's profile.</summary>
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = null!;
}