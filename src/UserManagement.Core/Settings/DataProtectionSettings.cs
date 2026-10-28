namespace UserManagement.Core.Settings;

public class DataProtectionSettings
{
    public string CertificateBase64 { get; set; } = string.Empty;
    public string CertificatePassword { get; set; } = string.Empty;
}
