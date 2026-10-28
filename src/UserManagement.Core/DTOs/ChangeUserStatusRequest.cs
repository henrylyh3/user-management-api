using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.DTOs;

public sealed class ChangeUserStatusRequest
{
    [Required, RegularExpression("^(Active|Disabled)$")]
    public string Status { get; init; } = string.Empty;
}
