using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.DTOs;

public sealed class ResetPasswordRequest
{
    [Required, StringLength(256)]
    public string Token { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
