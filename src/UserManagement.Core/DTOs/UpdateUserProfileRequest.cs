using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.DTOs;

public sealed class UpdateUserProfileRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;
}
