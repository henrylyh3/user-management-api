using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.DTOs;

public sealed class AccountTokenRequest
{
    [Required, StringLength(256)]
    public string Token { get; set; } = string.Empty;
}
