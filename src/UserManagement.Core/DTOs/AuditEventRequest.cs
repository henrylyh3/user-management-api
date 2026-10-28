using UserManagement.Core.Models;

namespace UserManagement.Core.DTOs;

public sealed class AuditEventRequest
{
    public required string Module { get; init; }
    public required string Action { get; init; }
    public AuditOutcome Outcome { get; init; }
    public string? ActorUserId { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
