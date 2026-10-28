using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UserManagement.Core.Models;

public enum AccountTokenPurpose
{
    EmailVerification,
    PasswordReset
}

public sealed class AccountToken : AuditableEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("UserId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("TokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    [BsonElement("Purpose")]
    [BsonRepresentation(BsonType.String)]
    public AccountTokenPurpose Purpose { get; set; }

    [BsonElement("ExpiresAt")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("ConsumedOn")]
    public DateTime? ConsumedOn { get; set; }
}
