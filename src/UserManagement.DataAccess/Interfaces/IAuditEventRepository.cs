using UserManagement.Core.DTOs;
using UserManagement.Core.Models;

namespace UserManagement.DataAccess.Interfaces;

public interface IAuditEventRepository
{
    Task CreateAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AuditEvent> Items, long TotalCount)> GetAsync(AuditEventQuery query, CancellationToken cancellationToken = default);
}
