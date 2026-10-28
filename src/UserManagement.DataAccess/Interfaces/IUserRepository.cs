using UserManagement.Core.Models;

namespace UserManagement.DataAccess.Interfaces;

public interface IUserRepository
{
    Task<List<User>> GetUsersAsync(int skip, int take);
    Task<long> CountUsersAsync();
    Task<User?> GetByIdAsync(string id);
    Task<long> CountActiveAdminsAsync();
    Task PingAsync(CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(string email);
    Task CreateAsync(User user);
    Task<User?> UpdateProfileAsync(string id, string firstName, string lastName);
    Task<User?> UpdateImageUrlAsync(string id, string imageUrl);
    Task<User?> RemoveImageUrlAsync(string id);
    Task<User?> IncrementFailedSigninAttemptsAsync(string id);
    Task ResetSigninFailuresAsync(string id);
    Task SetLastSignInOnAsync(string id, DateTime lastSignInOn);
    Task SetLockoutEndAsync(string id, DateTime lockoutEnd);
    Task SetEmailVerifiedAsync(string id);
    Task<bool> UpdatePasswordHashAndIncrementTokenVersionAsync(string id, string passwordHash);
    Task<bool> TryAcquireAccountEmailCooldownAsync(string id, AccountTokenPurpose purpose, DateTime now, DateTime nextAllowedOn, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<User?> ChangeRoleAsync(string id, string role, CancellationToken cancellationToken = default);
    Task<User?> ChangeStatusAsync(string id, AccountStatus status, string changedByUserId, CancellationToken cancellationToken = default);
    Task<User?> UnlockAsync(string id, CancellationToken cancellationToken = default);
}
