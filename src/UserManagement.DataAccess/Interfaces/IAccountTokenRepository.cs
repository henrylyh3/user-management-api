using UserManagement.Core.Models;

namespace UserManagement.DataAccess.Interfaces;

public interface IAccountTokenRepository
{
    Task CreateAsync(AccountToken accountToken, CancellationToken cancellationToken = default);
    Task<AccountToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> TryConsumeAsync(string id, DateTime consumedOn, CancellationToken cancellationToken = default);
    Task RevokeActiveByUserIdAndPurposeAsync(string userId, AccountTokenPurpose purpose, DateTime revokedOn, CancellationToken cancellationToken = default);
    Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
