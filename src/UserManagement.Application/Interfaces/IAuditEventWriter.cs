using UserManagement.Core.DTOs;

namespace UserManagement.Application.Interfaces;

public interface IAuditEventWriter
{
    Task WriteAsync(AuditEventRequest request, CancellationToken cancellationToken = default);
}
