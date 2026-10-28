using System.ComponentModel.DataAnnotations;
using UserManagement.Core.Settings;

namespace UserManagement.UnitTests.Validation;

public class SettingsValidationTests
{
    [Fact]
    public void MongoDbSettings_MissingOrInvalidValues_IsInvalid()
    {
        var settings = new MongoDbSettings { ConnectionString = "https://example.com", DatabaseName = string.Empty };

        Assert.NotEmpty(Validate(settings));
    }

    [Fact]
    public void CloudflareR2Settings_MissingOrInsecureValues_IsInvalid()
    {
        var settings = new CloudflareR2Settings
        {
            AccessKeyId = string.Empty,
            SecretAccessKey = string.Empty,
            BucketName = string.Empty,
            PublicBaseUrl = "http://example.com",
            S3Endpoint = "not-a-url"
        };

        Assert.NotEmpty(Validate(settings));
    }

    [Fact]
    public void AccountSecuritySettings_InvalidValues_IsInvalid()
    {
        var settings = new AccountSecuritySettings { FrontendBaseUrl = "not-a-url", PasswordResetTokenExpiryMinutes = 1, AccountEmailCooldownSeconds = 1, MaxFailedSigninAttempts = 2 };

        Assert.NotEmpty(Validate(settings));
    }

    [Fact]
    public void ResendSettings_MissingOrInvalidValues_IsInvalid()
    {
        var settings = new ResendSettings { ApiKey = "invalid", FromEmail = "invalid", FromName = string.Empty };

        Assert.NotEmpty(Validate(settings));
    }

    [Fact]
    public void AuditSettings_InvalidRetention_IsInvalid()
    {
        Assert.NotEmpty(Validate(new AuditSettings { RetentionDays = 0 }));
    }

    [Fact]
    public void CaptchaSettings_MissingSecretOrHostnames_IsInvalid()
    {
        Assert.NotEmpty(Validate(new CaptchaSettings { SecretKey = string.Empty, AllowedHostnames = [] }));
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
