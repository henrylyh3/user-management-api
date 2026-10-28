using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;

namespace UserManagement.DataAccess.Database;

public sealed class MongoIndexInitializer
{
    public const int SchemaVersion = 5;

    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<RefreshToken> _refreshTokens;
    private readonly IMongoCollection<AccountToken> _accountTokens;
    private readonly IMongoCollection<AuditEvent> _auditEvents;
    private readonly AuditSettings _auditSettings;
    private readonly IMongoDatabase _database;

    public MongoIndexInitializer(IOptions<MongoDbSettings> options, IOptions<AuditSettings> auditOptions)
    {
        var client = new MongoClient(options.Value.ConnectionString);
        _database = client.GetDatabase(options.Value.DatabaseName);
        _users = _database.GetCollection<User>("users");
        _refreshTokens = _database.GetCollection<RefreshToken>("refreshTokens");
        _accountTokens = _database.GetCollection<AccountToken>("accountTokens");
        _auditEvents = _database.GetCollection<AuditEvent>("auditEvents");
        _auditSettings = auditOptions.Value;
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        await MigrateAuditFieldsAsync(cancellationToken);

        await ReconcileCollectionAsync(_users,
        [
            new MongoIndexDefinition<User>("app_users_email_unique", new BsonDocument("Email", 1), unique: true)
        ], cancellationToken);

        await ReconcileCollectionAsync(_refreshTokens,
        [
            new MongoIndexDefinition<RefreshToken>("app_refresh_tokens_token_hash_unique", new BsonDocument("TokenHash", 1), unique: true),
            new MongoIndexDefinition<RefreshToken>("app_refresh_tokens_user_id", new BsonDocument("UserId", 1)),
            new MongoIndexDefinition<RefreshToken>("app_refresh_tokens_user_session", new BsonDocument { { "UserId", 1 }, { "SessionId", 1 } }),
            new MongoIndexDefinition<RefreshToken>("app_refresh_tokens_expires_at_ttl", new BsonDocument("ExpiresAt", 1), expireAfter: TimeSpan.Zero)
        ], cancellationToken);

        await ReconcileCollectionAsync(_accountTokens,
        [
            new MongoIndexDefinition<AccountToken>("app_account_tokens_token_hash_unique", new BsonDocument("TokenHash", 1), unique: true),
            new MongoIndexDefinition<AccountToken>("app_account_tokens_user_purpose", new BsonDocument { { "UserId", 1 }, { "Purpose", 1 } }),
            new MongoIndexDefinition<AccountToken>("app_account_tokens_expires_at_ttl", new BsonDocument("ExpiresAt", 1), expireAfter: TimeSpan.Zero)
        ], cancellationToken);

        await ReconcileCollectionAsync(_auditEvents,
        [
            new MongoIndexDefinition<AuditEvent>("app_audit_events_occurred_on_ttl", new BsonDocument("OccurredOn", 1), expireAfter: TimeSpan.FromDays(_auditSettings.RetentionDays)),
            new MongoIndexDefinition<AuditEvent>("app_audit_events_actor_occurred_on", new BsonDocument { { "ActorUserId", 1 }, { "OccurredOn", -1 } }),
            new MongoIndexDefinition<AuditEvent>("app_audit_events_module_action_occurred_on", new BsonDocument { { "Module", 1 }, { "Action", 1 }, { "OccurredOn", -1 } }),
            new MongoIndexDefinition<AuditEvent>("app_audit_events_resource_occurred_on", new BsonDocument { { "ResourceType", 1 }, { "ResourceId", 1 }, { "OccurredOn", -1 } }),
            new MongoIndexDefinition<AuditEvent>("app_audit_events_trace_id", new BsonDocument("TraceId", 1))
        ], cancellationToken);
    }

    private async Task MigrateAuditFieldsAsync(CancellationToken cancellationToken)
    {
        await BackfillAuditFieldsAsync("users", "$CreatedOn", new BsonDocument("$ifNull", new BsonArray { "$ModifiedOn", new BsonDocument("$ifNull", new BsonArray { "$UpdatedOn", new BsonDocument("$ifNull", new BsonArray { "$CreatedOn", "$$NOW" }) }) }), cancellationToken, includeStatus: true, removeUpdatedOn: true);
        await BackfillAuditFieldsAsync("refreshTokens", "$CreatedOn", new BsonDocument("$ifNull", new BsonArray { "$ModifiedOn", new BsonDocument("$ifNull", new BsonArray { "$RevokedOn", new BsonDocument("$ifNull", new BsonArray { "$LastSeenOn", new BsonDocument("$ifNull", new BsonArray { "$CreatedOn", "$$NOW" }) }) }) }), cancellationToken);
        await BackfillAuditFieldsAsync("accountTokens", "$CreatedOn", new BsonDocument("$ifNull", new BsonArray { "$ModifiedOn", new BsonDocument("$ifNull", new BsonArray { "$ConsumedOn", new BsonDocument("$ifNull", new BsonArray { "$CreatedOn", "$$NOW" }) }) }), cancellationToken);
        await BackfillAuditFieldsAsync("auditEvents", "$OccurredOn", new BsonDocument("$ifNull", new BsonArray { "$ModifiedOn", new BsonDocument("$ifNull", new BsonArray { "$OccurredOn", "$$NOW" }) }), cancellationToken);
        await BackfillAuditFieldsAsync("DataProtectionKeys", "$CreatedOn", new BsonDocument("$ifNull", new BsonArray { "$ModifiedOn", new BsonDocument("$ifNull", new BsonArray { "$CreatedOn", "$$NOW" }) }), cancellationToken);
    }

    private async Task BackfillAuditFieldsAsync(string collectionName, string createdOnSource, BsonValue modifiedOnExpression, CancellationToken cancellationToken, bool includeStatus = false, bool removeUpdatedOn = false)
    {
        var collection = _database.GetCollection<BsonDocument>(collectionName);
        var fields = new BsonDocument
        {
            { "CreatedOn", new BsonDocument("$ifNull", new BsonArray { createdOnSource, "$$NOW" }) },
            { "ModifiedOn", modifiedOnExpression },
            { "CreatedBy", new BsonDocument("$ifNull", new BsonArray { "$CreatedBy", AuditActors.System }) },
            { "ModifiedBy", new BsonDocument("$ifNull", new BsonArray { "$ModifiedBy", AuditActors.System }) }
        };

        if (includeStatus) fields.Add("Status", new BsonDocument("$ifNull", new BsonArray { "$Status", AccountStatus.Active.ToString() }));

        var stages = new List<BsonDocument> { new("$set", fields) };
        if (removeUpdatedOn) stages.Add(new BsonDocument("$unset", "UpdatedOn"));
        var incompleteFields = new BsonArray
        {
            new BsonDocument("CreatedOn", BsonNull.Value),
            new BsonDocument("ModifiedOn", BsonNull.Value),
            new BsonDocument("CreatedBy", BsonNull.Value),
            new BsonDocument("ModifiedBy", BsonNull.Value)
        };
        if (includeStatus) incompleteFields.Add(new BsonDocument("Status", BsonNull.Value));
        if (removeUpdatedOn) incompleteFields.Add(new BsonDocument("UpdatedOn", new BsonDocument("$exists", true)));

        await collection.UpdateManyAsync(new BsonDocument("$or", incompleteFields), Builders<BsonDocument>.Update.Pipeline(stages), cancellationToken: cancellationToken);
    }

    private static async Task ReconcileCollectionAsync<T>(IMongoCollection<T> collection, IReadOnlyCollection<MongoIndexDefinition<T>> desiredIndexes, CancellationToken cancellationToken)
    {
        List<BsonDocument> existingIndexes;

        try
        {
            using var cursor = await collection.Indexes.ListAsync(cancellationToken);
            existingIndexes = await cursor.ToListAsync(cancellationToken);
        }
        catch (MongoCommandException exception) when (exception.Code == 26)
        {
            existingIndexes = [];
        }

        var desiredByName = desiredIndexes.ToDictionary(index => index.Name, StringComparer.Ordinal);

        foreach (var existingIndex in existingIndexes)
        {
            var existingName = existingIndex["name"].AsString;

            if (existingName == "_id_")
            {
                continue;
            }

            if (!desiredByName.TryGetValue(existingName, out var desiredIndex) || !desiredIndex.Matches(existingIndex))
            {
                try
                {
                    await collection.Indexes.DropOneAsync(existingName, cancellationToken);
                }
                catch (MongoCommandException exception) when (exception.Code == 27)
                {
                    // Another application instance already removed this index during startup.
                }
            }
        }

        await collection.Indexes.CreateManyAsync(desiredIndexes.Select(index => index.CreateModel()), cancellationToken: cancellationToken);
    }

    private sealed class MongoIndexDefinition<T>(string name, BsonDocument keys, bool unique = false, TimeSpan? expireAfter = null)
    {
        public string Name { get; } = name;

        public CreateIndexModel<T> CreateModel()
        {
            return new CreateIndexModel<T>(new BsonDocumentIndexKeysDefinition<T>(keys), new CreateIndexOptions { Name = Name, Unique = unique, ExpireAfter = expireAfter });
        }

        public bool Matches(BsonDocument existingIndex)
        {
            if (!existingIndex.TryGetValue("key", out var existingKeys) || !existingKeys.AsBsonDocument.Equals(keys))
            {
                return false;
            }

            var existingUnique = existingIndex.TryGetValue("unique", out var uniqueValue) && uniqueValue.ToBoolean();

            if (existingUnique != unique)
            {
                return false;
            }

            var hasExistingExpiry = existingIndex.TryGetValue("expireAfterSeconds", out var expiryValue);

            if (expireAfter is null)
            {
                return !hasExistingExpiry;
            }

            return hasExistingExpiry && expiryValue.ToInt64() == Convert.ToInt64(expireAfter.Value.TotalSeconds);
        }
    }
}
