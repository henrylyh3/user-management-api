using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.DTOs;

public class SigninRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    public bool KeepMeSignedIn { get; set; }

    [StringLength(2048)]
    public string? CaptchaToken { get; set; }
}
