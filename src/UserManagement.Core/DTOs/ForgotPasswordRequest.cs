using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.DTOs;

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [StringLength(2048)]
    public string? CaptchaToken { get; set; }
}
