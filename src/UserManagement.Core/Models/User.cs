using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UserManagement.Core.Models;

public class User
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

    [BsonElement("CreatedOn")]
    public DateTime CreatedOn { get; set; }

    [BsonElement("UpdatedOn")]
    public DateTime UpdatedOn { get; set; }

    [BsonElement("PasswordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("Role")]
    public string Role { get; set; } = "User";
    //User(default), Admin

    [BsonElement("ImageUrl")]
    public string? ImageUrl { get; set; }
}