using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;
using UserManagement.DataAccess.Database;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.DataAccess.Repositories;

public sealed class AuditEventRepository : MongoDBClient, IAuditEventRepository
{
    private readonly IMongoCollection<AuditEvent> _auditEvents;
    private readonly IAuditActorProvider _auditActorProvider;

    public AuditEventRepository(IOptions<MongoDbSettings> options, IAuditActorProvider auditActorProvider) : base(options)
    {
        _auditActorProvider = auditActorProvider;
        _auditEvents = MongoDb.GetCollection<AuditEvent>("auditEvents");
    }

    public async Task CreateAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        AuditStamp.Created(auditEvent, _auditActorProvider.ActorId);
        await _auditEvents.InsertOneAsync(auditEvent, cancellationToken: cancellationToken);
    }

    public async Task<(IReadOnlyList<AuditEvent> Items, long TotalCount)> GetAsync(AuditEventQuery query, CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<AuditEvent>>();

        if (!string.IsNullOrWhiteSpace(query.Module)) filters.Add(Builders<AuditEvent>.Filter.Eq(auditEvent => auditEvent.Module, query.Module.Trim()));
        if (!string.IsNullOrWhiteSpace(query.Action)) filters.Add(Builders<AuditEvent>.Filter.Eq(auditEvent => auditEvent.Action, query.Action.Trim()));
        if (query.Outcome.HasValue) filters.Add(Builders<AuditEvent>.Filter.Eq(auditEvent => auditEvent.Outcome, query.Outcome.Value));
        if (!string.IsNullOrWhiteSpace(query.ActorUserId)) filters.Add(Builders<AuditEvent>.Filter.Eq(auditEvent => auditEvent.ActorUserId, query.ActorUserId.Trim()));
        if (!string.IsNullOrWhiteSpace(query.ResourceType)) filters.Add(Builders<AuditEvent>.Filter.Eq(auditEvent => auditEvent.ResourceType, query.ResourceType.Trim()));
        if (!string.IsNullOrWhiteSpace(query.ResourceId)) filters.Add(Builders<AuditEvent>.Filter.Eq(auditEvent => auditEvent.ResourceId, query.ResourceId.Trim()));
        if (query.From.HasValue) filters.Add(Builders<AuditEvent>.Filter.Gte(auditEvent => auditEvent.OccurredOn, query.From.Value.ToUniversalTime()));
        if (query.To.HasValue) filters.Add(Builders<AuditEvent>.Filter.Lte(auditEvent => auditEvent.OccurredOn, query.To.Value.ToUniversalTime()));

        var filter = filters.Count == 0 ? Builders<AuditEvent>.Filter.Empty : Builders<AuditEvent>.Filter.And(filters);
        var skip = (query.Page - 1) * query.PageSize;
        var itemsTask = _auditEvents.Find(filter).SortByDescending(auditEvent => auditEvent.OccurredOn).Skip(skip).Limit(query.PageSize).ToListAsync(cancellationToken);
        var countTask = _auditEvents.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        await Task.WhenAll(itemsTask, countTask);

        return (itemsTask.Result, countTask.Result);
    }
}
