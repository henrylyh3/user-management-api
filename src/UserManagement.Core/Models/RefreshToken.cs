using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UserManagement.Core.Models;

public sealed class RefreshToken : AuditableEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("UserId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("TokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    [BsonElement("ExpiresAt")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("RevokedOn")]
    public DateTime? RevokedOn { get; set; }

    [BsonElement("ReplacedByTokenHash")]
    public string? ReplacedByTokenHash { get; set; }

    [BsonElement("KeepMeSignedIn")]
    public bool KeepMeSignedIn { get; set; }

    [BsonElement("SessionId")]
    public string SessionId { get; set; } = string.Empty;

    [BsonElement("SessionCreatedOn")]
    public DateTime SessionCreatedOn { get; set; }

    [BsonElement("LastSeenOn")]
    public DateTime LastSeenOn { get; set; }

    [BsonElement("UserAgent")]
    public string UserAgent { get; set; } = string.Empty;

    [BsonElement("IpAddress")]
    public string IpAddress { get; set; } = string.Empty;
}
