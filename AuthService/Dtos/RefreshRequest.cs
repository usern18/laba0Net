using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

/// <summary>Refresh credential to rotate or revoke.</summary>
public class RefreshRequest
{
    /// <summary>Opaque refresh token returned by login or refresh.</summary>
    [Required]
    public string RefreshToken { get; set; } = null!;
}