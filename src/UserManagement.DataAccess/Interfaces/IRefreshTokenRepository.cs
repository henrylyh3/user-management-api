using UserManagement.Core.Models;

namespace UserManagement.DataAccess.Interfaces;

public interface IRefreshTokenRepository
{
    Task CreateAsync(RefreshToken refreshToken);
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);
    Task<bool> TryRevokeAsync(string id, string? replacedByTokenHash = null);
    Task RevokeAllByUserIdAsync(string userId);
    Task<List<RefreshToken>> GetActiveByUserIdAsync(string userId);
    Task<bool> IsSessionActiveAsync(string userId, string sessionId);
    Task<bool> RevokeSessionAsync(string userId, string sessionId);
    Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
