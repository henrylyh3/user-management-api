using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.Settings;

public sealed class CaptchaSettings
{
    [Required, StringLength(256)]
    public string SecretKey { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string[] AllowedHostnames { get; set; } = [];

    [Range(1, 19)]
    public int SigninRequiredAfterFailedAttempts { get; set; } = 2;
}
