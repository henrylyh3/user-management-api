using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.Settings;

public sealed class AuditSettings
{
    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 90;
}
