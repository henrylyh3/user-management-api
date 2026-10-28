using MongoDB.Bson.Serialization.Attributes;

namespace UserManagement.Core.Models;

public abstract class AuditableEntity
{
    [BsonElement("CreatedOn")]
    public DateTime CreatedOn { get; set; }

    [BsonElement("ModifiedOn")]
    public DateTime ModifiedOn { get; set; }

    [BsonElement("CreatedBy")]
    public string CreatedBy { get; set; } = AuditActors.System;

    [BsonElement("ModifiedBy")]
    public string ModifiedBy { get; set; } = AuditActors.System;
}
