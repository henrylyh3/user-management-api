namespace UserManagement.Core.DTOs;

public sealed class AdminUserDto
{
    public string Id { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? LockoutEnd { get; init; }
    public bool IsLockedOut { get; init; }
    public DateTime? LastSignInOn { get; init; }
    public DateTime CreatedOn { get; init; }
    public DateTime ModifiedOn { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string ModifiedBy { get; init; } = string.Empty;
}
