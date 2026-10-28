using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;

namespace UserManagement.Web.Services;

public sealed class MongoDataProtectionKeyRepository : IXmlRepository
{
    private readonly IMongoCollection<DataProtectionKeyDocument> _keys;

    public MongoDataProtectionKeyRepository(MongoDbSettings settings)
    {
        var database = new MongoClient(settings.ConnectionString).GetDatabase(settings.DatabaseName);
        _keys = database.GetCollection<DataProtectionKeyDocument>("DataProtectionKeys");
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        return _keys.Find(FilterDefinition<DataProtectionKeyDocument>.Empty)
            .ToList()
            .Select(key => XElement.Parse(key.Xml))
            .ToArray();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        var now = DateTime.UtcNow;
        _keys.InsertOne(new DataProtectionKeyDocument
        {
            FriendlyName = friendlyName,
            Xml = element.ToString(SaveOptions.DisableFormatting),
            CreatedOn = now,
            ModifiedOn = now,
            CreatedBy = AuditActors.System,
            ModifiedBy = AuditActors.System
        });
    }

    private sealed class DataProtectionKeyDocument
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("FriendlyName")]
        public string FriendlyName { get; set; } = string.Empty;

        [BsonElement("Xml")]
        public string Xml { get; set; } = string.Empty;

        [BsonElement("CreatedOn")]
        public DateTime CreatedOn { get; set; }

        [BsonElement("ModifiedOn")]
        public DateTime ModifiedOn { get; set; }

        [BsonElement("CreatedBy")]
        public string CreatedBy { get; set; } = AuditActors.System;

        [BsonElement("ModifiedBy")]
        public string ModifiedBy { get; set; } = AuditActors.System;
    }
}
