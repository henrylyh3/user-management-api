using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.Settings;

public class JwtSettings
{
    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; }

    [Required]
    [MinLength(32)]
    public string Key { get; set; } = string.Empty;
}
