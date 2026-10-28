using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UserManagement.Core.Models;

public enum AuditOutcome
{
    Success,
    Failure,
    Denied
}

public sealed class AuditEvent : AuditableEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public DateTime OccurredOn { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public AuditOutcome Outcome { get; set; }

    public string? ActorUserId { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? TraceId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}
