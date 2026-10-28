using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.Settings;

public sealed class RefreshTokenSettings
{
    [Range(1, 30)]
    public int ExpiryDays { get; set; } = 7;

    [Required]
    public string CookieName { get; set; } = "refresh_token";
}
