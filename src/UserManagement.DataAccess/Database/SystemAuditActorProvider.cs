using UserManagement.Core.Models;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.DataAccess.Database;

public sealed class SystemAuditActorProvider : IAuditActorProvider
{
    public string ActorId => AuditActors.System;
}
