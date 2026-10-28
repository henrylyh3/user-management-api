using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.Settings;

public sealed class ResendSettings
{
    [Required, RegularExpression(@"^re_.+", ErrorMessage = "Resend API key must start with re_.")]
    public string ApiKey { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string FromEmail { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string FromName { get; set; } = "User Management";
}
