using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.DTOs;

public sealed class UpdateUserProfileRequest
{
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;
}