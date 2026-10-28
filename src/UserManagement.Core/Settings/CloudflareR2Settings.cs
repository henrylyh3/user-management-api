using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.Settings;

public sealed class CloudflareR2Settings
{
    [Required]
    public string AccessKeyId { get; set; } = string.Empty;

    [Required]
    public string SecretAccessKey { get; set; } = string.Empty;

    [Required]
    public string BucketName { get; set; } = string.Empty;

    [Required, Url]
    [RegularExpression(@"^https://.+", ErrorMessage = "Cloudflare R2 public base URL must use HTTPS.")]
    public string PublicBaseUrl { get; set; } = string.Empty;

    [Required, Url]
    [RegularExpression(@"^https://.+", ErrorMessage = "Cloudflare R2 S3 endpoint must use HTTPS.")]
    public string S3Endpoint { get; set; } = string.Empty;
}
