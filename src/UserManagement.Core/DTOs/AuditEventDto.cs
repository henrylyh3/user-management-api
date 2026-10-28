namespace UserManagement.Core.DTOs;

public sealed class AuditEventDto
{
    public string Id { get; init; } = string.Empty;
    public DateTime OccurredOn { get; init; }
    public string Module { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string? ActorUserId { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public string? TraceId { get; init; }
}
