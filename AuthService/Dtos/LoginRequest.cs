using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

/// <summary>Credentials used to authenticate a user.</summary>
public class LoginRequest
{
    /// <summary>Registered email address.</summary>
    [Required, EmailAddress]
    public string Email { get; set; } = null!;
    /// <summary>Plaintext password supplied for verification; never logged or stored.</summary>
    [Required]
    public string Password { get; set; } = null!;
}