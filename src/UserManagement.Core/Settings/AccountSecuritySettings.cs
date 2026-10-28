using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.Settings;

public sealed class AccountSecuritySettings
{
    [Required, Url]
    public string FrontendBaseUrl { get; set; } = string.Empty;

    [Range(5, 1440)]
    public int PasswordResetTokenExpiryMinutes { get; set; } = 30;

    [Range(5, 10080)]
    public int EmailVerificationTokenExpiryMinutes { get; set; } = 1440;

    [Range(30, 3600)]
    public int AccountEmailCooldownSeconds { get; set; } = 60;

    [Range(3, 20)]
    public int MaxFailedSigninAttempts { get; set; } = 5;

    [Range(1, 1440)]
    public int LockoutMinutes { get; set; } = 15;
}
