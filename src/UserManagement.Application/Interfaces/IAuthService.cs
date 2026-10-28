using UserManagement.Core.DTOs;

namespace UserManagement.Application.Interfaces;

public interface IAuthService
{
    Task<SigninResult> SigninAsync(SigninRequest request, ClientSessionInfo clientSession, CancellationToken cancellationToken = default);
    Task<AuthSession?> RefreshAsync(string refreshToken, ClientSessionInfo clientSession);
    Task SignoutAsync(string refreshToken);
    Task SignoutAllAsync(string userId);
    Task<IReadOnlyCollection<UserSessionDto>> GetSessionsAsync(string userId, string? currentSessionId);
    Task<RevokeSessionResult> RevokeSessionAsync(string userId, string sessionId, string? currentSessionId);
}
