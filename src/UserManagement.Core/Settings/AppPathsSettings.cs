namespace UserManagement.Core.Settings;

public sealed class AppPathsSettings
{
    public string LogDirectory { get; set; } = "/data/log/user-management-api";
    public string UploadDirectory { get; set; } = "/data/upload";
}
