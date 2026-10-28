using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.DTOs;

public sealed class ChangeUserRoleRequest
{
    [Required, RegularExpression("^(User|Admin)$")]
    public string Role { get; init; } = string.Empty;
}
