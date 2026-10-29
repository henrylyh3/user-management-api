using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.DTOs;

public sealed class DeleteAccountRequest
{
    [Required, StringLength(128)]
    public string CurrentPassword { get; set; } = string.Empty;
}
