using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.Application.Services;

public sealed class AuditEventService : IAuditEventService
{
    private readonly IAuditEventRepository _repository;

    public AuditEventService(IAuditEventRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<AuditEventDto>> GetAsync(AuditEventQuery query, CancellationToken cancellationToken = default)
    {
        var (events, totalCount) = await _repository.GetAsync(query, cancellationToken);
        var items = events.Select(auditEvent => new AuditEventDto
        {
            Id = auditEvent.Id,
            OccurredOn = auditEvent.OccurredOn,
            Module = auditEvent.Module,
            Action = auditEvent.Action,
            Outcome = auditEvent.Outcome.ToString(),
            ActorUserId = auditEvent.ActorUserId,
            ResourceType = auditEvent.ResourceType,
            ResourceId = auditEvent.ResourceId,
            TraceId = auditEvent.TraceId
        }).ToList();

        return new PagedResult<AuditEventDto> { Items = items, Page = query.Page, PageSize = query.PageSize, TotalCount = totalCount };
    }
}
