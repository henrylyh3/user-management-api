using System.Security.Claims;
using UserManagement.Core.Models;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.Web.Services;

public sealed class HttpAuditActorProvider(IHttpContextAccessor httpContextAccessor) : IAuditActorProvider
{
    public string ActorId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true ? user.FindFirstValue("sub") ?? AuditActors.System : AuditActors.System;
        }
    }
}
