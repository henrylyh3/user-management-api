using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;

namespace UserManagement.Web.Controllers;

[ApiController]
[Route("api/audit-events")]
[Authorize(Roles = UserRoles.Admin)]
public sealed class AuditEventsController : ControllerBase
{
    private readonly IAuditEventService _auditEventService;

    public AuditEventsController(IAuditEventService auditEventService)
    {
        _auditEventService = auditEventService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditEventDto>>> Get([FromQuery] AuditEventQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _auditEventService.GetAsync(query, cancellationToken));
    }
}
