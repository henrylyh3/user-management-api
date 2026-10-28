using UserManagement.Core.DTOs;

namespace UserManagement.Application.Interfaces;

public interface IAuditEventService
{
    Task<PagedResult<AuditEventDto>> GetAsync(AuditEventQuery query, CancellationToken cancellationToken = default);
}
