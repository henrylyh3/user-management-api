using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.DTOs;

public sealed class UpdateUserProfileRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}