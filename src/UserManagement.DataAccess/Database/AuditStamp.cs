using UserManagement.Core.Models;

namespace UserManagement.DataAccess.Database;

internal static class AuditStamp
{
    public static void Created(AuditableEntity entity, string? actorId)
    {
        var now = DateTime.UtcNow;
        var actor = NormalizeActor(actorId);
        entity.CreatedOn = now;
        entity.ModifiedOn = now;
        entity.CreatedBy = actor;
        entity.ModifiedBy = actor;
    }

    public static string NormalizeActor(string? actorId) => string.IsNullOrWhiteSpace(actorId) ? AuditActors.System : actorId.Trim();
}
