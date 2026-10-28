using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.Application.Services;

public sealed class AdminUserService(IUserRepository userRepository, IRefreshTokenRepository refreshTokenRepository, IAuditEventWriter auditEventWriter) : IAdminUserService
{
    public async Task<AdminUserMutationResult> ChangeRoleAsync(string actorUserId, string targetUserId, string role, CancellationToken cancellationToken = default)
    {
        if (!UserRoles.IsSupported(role)) return Conflict("invalid_role");
        var user = await userRepository.GetByIdAsync(targetUserId);
        if (user is null) return NotFound();
        if (user.Role == role) return Success();
        if (actorUserId == targetUserId) return Conflict("self_role_change");
        if (user.Role == UserRoles.Admin && user.Status == AccountStatus.Active && role == UserRoles.User && await userRepository.CountActiveAdminsAsync() <= 1) return Conflict("last_active_admin");

        var updated = await userRepository.ChangeRoleAsync(targetUserId, role, cancellationToken);
        if (updated is null) return NotFound();

        await refreshTokenRepository.RevokeAllByUserIdAsync(targetUserId);
        await WriteAuditAsync("RoleChanged", actorUserId, targetUserId, new Dictionary<string, string> { ["PreviousRole"] = user.Role, ["NewRole"] = role }, cancellationToken);
        return Success();
    }

    public async Task<AdminUserMutationResult> ChangeStatusAsync(string actorUserId, string targetUserId, AccountStatus status, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(targetUserId);
        if (user is null) return NotFound();
        if (user.Status == status) return Success();
        if (actorUserId == targetUserId && status == AccountStatus.Disabled) return Conflict("self_disable");
        if (user.Role == UserRoles.Admin && user.Status == AccountStatus.Active && status == AccountStatus.Disabled && await userRepository.CountActiveAdminsAsync() <= 1) return Conflict("last_active_admin");

        var updated = await userRepository.ChangeStatusAsync(targetUserId, status, actorUserId, cancellationToken);
        if (updated is null) return NotFound();

        await refreshTokenRepository.RevokeAllByUserIdAsync(targetUserId);
        await WriteAuditAsync("AccountStatusChanged", actorUserId, targetUserId, new Dictionary<string, string> { ["PreviousStatus"] = user.Status.ToString(), ["NewStatus"] = status.ToString() }, cancellationToken);
        return Success();
    }

    public async Task<AdminUserMutationResult> UnlockAsync(string actorUserId, string targetUserId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.UnlockAsync(targetUserId, cancellationToken);
        if (user is null) return NotFound();

        await WriteAuditAsync("AccountUnlocked", actorUserId, targetUserId, cancellationToken: cancellationToken);
        return Success();
    }

    private Task WriteAuditAsync(string action, string actorUserId, string targetUserId, IReadOnlyDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        return auditEventWriter.WriteAsync(new AuditEventRequest { Module = "UserAdministration", Action = action, Outcome = AuditOutcome.Success, ActorUserId = actorUserId, ResourceType = "User", ResourceId = targetUserId, Metadata = metadata }, cancellationToken);
    }

    private static AdminUserMutationResult Success() => new() { Status = AdminUserMutationStatus.Success };
    private static AdminUserMutationResult NotFound() => new() { Status = AdminUserMutationStatus.NotFound };
    private static AdminUserMutationResult Conflict(string code) => new() { Status = AdminUserMutationStatus.Conflict, ConflictCode = code };
}
