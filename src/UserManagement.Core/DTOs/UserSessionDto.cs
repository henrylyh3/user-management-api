namespace UserManagement.Core.DTOs;

public sealed class UserSessionDto
{
    public string SessionId { get; init; } = string.Empty;
    public DateTime CreatedOn { get; init; }
    public DateTime LastSeenOn { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string UserAgent { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public bool IsCurrent { get; init; }
}
