using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UserManagement.Core.Models;

public class User : AuditableEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [BsonElement("LastName")]
    public string LastName { get; set; } = string.Empty;

    [BsonElement("Email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("PasswordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("Role")]
    public string Role { get; set; } = UserRoles.User;

    [BsonElement("Status")]
    [BsonRepresentation(BsonType.String)]
    public AccountStatus Status { get; set; } = AccountStatus.Active;

    [BsonElement("DisabledOn")]
    public DateTime? DisabledOn { get; set; }

    [BsonElement("DisabledByUserId")]
    public string? DisabledByUserId { get; set; }

    [BsonElement("ImageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("IsEmailVerified")]
    public bool IsEmailVerified { get; set; } = true;

    [BsonElement("FailedSigninAttempts")]
    public int FailedSigninAttempts { get; set; }

    [BsonElement("LockoutEnd")]
    public DateTime? LockoutEnd { get; set; }

    [BsonElement("LastSignInOn")]
    public DateTime? LastSignInOn { get; set; }

    [BsonElement("TokenVersion")]
    public int TokenVersion { get; set; }

    [BsonElement("NextEmailVerificationAllowedOn")]
    public DateTime? NextEmailVerificationAllowedOn { get; set; }

    [BsonElement("NextPasswordResetAllowedOn")]
    public DateTime? NextPasswordResetAllowedOn { get; set; }
}
