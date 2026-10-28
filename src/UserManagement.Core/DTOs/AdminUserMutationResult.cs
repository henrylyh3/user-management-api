namespace UserManagement.Core.DTOs;

public enum AdminUserMutationStatus
{
    Success,
    NotFound,
    Conflict
}

public sealed class AdminUserMutationResult
{
    public AdminUserMutationStatus Status { get; init; }
    public string? ConflictCode { get; init; }
}
