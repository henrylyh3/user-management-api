using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.DTOs;

public class SigninRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}