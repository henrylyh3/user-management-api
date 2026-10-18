namespace UserManagement.Core.Settings;

public sealed class CloudflareR2Settings
{
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string S3Endpoint { get; set; } = string.Empty;
}