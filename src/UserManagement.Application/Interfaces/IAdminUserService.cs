using UserManagement.Core.DTOs;
using UserManagement.Core.Models;

namespace UserManagement.Application.Interfaces;

public interface IAdminUserService
{
    Task<AdminUserMutationResult> ChangeRoleAsync(string actorUserId, string targetUserId, string role, CancellationToken cancellationToken = default);
    Task<AdminUserMutationResult> ChangeStatusAsync(string actorUserId, string targetUserId, AccountStatus status, CancellationToken cancellationToken = default);
    Task<AdminUserMutationResult> UnlockAsync(string actorUserId, string targetUserId, CancellationToken cancellationToken = default);
}
