using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _repository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenService _tokenService;
    private readonly RefreshTokenSettings _refreshTokenSettings;
    private readonly AccountSecuritySettings _accountSecuritySettings;
    private readonly CaptchaSettings _captchaSettings;
    private readonly ICaptchaService _captchaService;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository repository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenService tokenService,
        IOptions<RefreshTokenSettings> refreshTokenOptions,
        IOptions<AccountSecuritySettings> accountSecurityOptions,
        IOptions<CaptchaSettings> captchaOptions,
        ICaptchaService captchaService,
        IAuditEventWriter auditEventWriter,
        ILogger<AuthService> logger)
    {
        _repository = repository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenSettings = refreshTokenOptions.Value;
        _accountSecuritySettings = accountSecurityOptions.Value;
        _captchaSettings = captchaOptions.Value;
        _captchaService = captchaService;
        _auditEventWriter = auditEventWriter;
        _logger = logger;
    }

    public async Task<SigninResult> SigninAsync(SigninRequest request, ClientSessionInfo clientSession, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _repository.GetByEmailAsync(email);

        if (user is null)
        {
            await WriteAuditAsync("SignIn", AuditOutcome.Failure);
            return new SigninResult { Status = SigninStatus.InvalidCredentials };
        }

        if (user.LockoutEnd > DateTime.UtcNow)
        {
            await WriteAuditAsync("SignIn", AuditOutcome.Denied, user.Id, "User", user.Id, new Dictionary<string, string> { ["Reason"] = "AccountLocked" });
            return new SigninResult { Status = SigninStatus.LockedOut };
        }

        if (user.LockoutEnd is not null)
        {
            await _repository.ResetSigninFailuresAsync(user.Id);
            user.FailedSigninAttempts = 0;
            user.LockoutEnd = null;
        }

        if (user.FailedSigninAttempts >= _captchaSettings.SigninRequiredAfterFailedAttempts && !await _captchaService.ValidateAsync(request.CaptchaToken, "signin", clientSession.IpAddress, cancellationToken))
        {
            await WriteAuditAsync("SignIn", AuditOutcome.Denied, user.Id, "User", user.Id, new Dictionary<string, string> { ["Reason"] = "CaptchaRequired" });
            return new SigninResult { Status = SigninStatus.CaptchaRequired, CaptchaRequired = true };
        }

        var result = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password
        );

        if (result == PasswordVerificationResult.Failed)
        {
            var updatedUser = await _repository.IncrementFailedSigninAttemptsAsync(user.Id);

            if (updatedUser is not null && updatedUser.FailedSigninAttempts >= _accountSecuritySettings.MaxFailedSigninAttempts)
            {
                await _repository.SetLockoutEndAsync(user.Id, DateTime.UtcNow.AddMinutes(_accountSecuritySettings.LockoutMinutes));
                _logger.LogWarning("Account temporarily locked after repeated signin failures for user {UserId}", user.Id);
                await WriteAuditAsync("AccountLocked", AuditOutcome.Success, user.Id, "User", user.Id);
                return new SigninResult { Status = SigninStatus.LockedOut };
            }

            await WriteAuditAsync("SignIn", AuditOutcome.Failure, user.Id, "User", user.Id);
            return new SigninResult { Status = SigninStatus.InvalidCredentials, CaptchaRequired = updatedUser?.FailedSigninAttempts >= _captchaSettings.SigninRequiredAfterFailedAttempts };
        }

        if (user.Status == AccountStatus.Disabled)
        {
            await WriteAuditAsync("SignIn", AuditOutcome.Denied, user.Id, "User", user.Id, new Dictionary<string, string> { ["Reason"] = "AccountDisabled" });
            return new SigninResult { Status = SigninStatus.AccountDisabled };
        }

        if (user.FailedSigninAttempts > 0)
        {
            await _repository.ResetSigninFailuresAsync(user.Id);
        }

        if (!user.IsEmailVerified)
        {
            await WriteAuditAsync("SignIn", AuditOutcome.Denied, user.Id, "User", user.Id, new Dictionary<string, string> { ["Reason"] = "EmailNotVerified" });
            return new SigninResult { Status = SigninStatus.EmailVerificationRequired };
        }

        await _repository.SetLastSignInOnAsync(user.Id, DateTime.UtcNow);
        var session = await CreateSessionAsync(user, request.KeepMeSignedIn, clientSession);
        await WriteAuditAsync("SignIn", AuditOutcome.Success, user.Id, "User", user.Id);
        return new SigninResult { Status = SigninStatus.Success, Session = session };
    }

    public async Task<AuthSession?> RefreshAsync(string refreshToken, ClientSessionInfo clientSession)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var tokenHash = HashToken(refreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);

        if (storedToken is null || storedToken.ExpiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        if (IsRotatedToken(storedToken))
        {
            await HandleRefreshTokenReuseAsync(storedToken.UserId);
            return null;
        }

        if (storedToken.RevokedOn is not null)
        {
            return null;
        }

        var user = await _repository.GetByIdAsync(storedToken.UserId);

        if (user is null || user.Status == AccountStatus.Disabled)
        {
            await _refreshTokenRepository.TryRevokeAsync(storedToken.Id);
            return null;
        }

        var nextRefreshToken = CreateRefreshToken();
        var nextTokenHash = HashToken(nextRefreshToken);
        var revoked = await _refreshTokenRepository.TryRevokeAsync(storedToken.Id, nextTokenHash);

        if (!revoked)
        {
            var latestToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);

            if (latestToken is not null && IsRotatedToken(latestToken))
            {
                await HandleRefreshTokenReuseAsync(latestToken.UserId);
            }

            return null;
        }

        return await CreateSessionAsync(user, storedToken.KeepMeSignedIn, clientSession, nextRefreshToken, nextTokenHash, storedToken);
    }

    public async Task SignoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(HashToken(refreshToken));

        if (storedToken is not null)
        {
            var revoked = await _refreshTokenRepository.TryRevokeAsync(storedToken.Id);
            await WriteAuditAsync("SignOut", revoked ? AuditOutcome.Success : AuditOutcome.Failure, storedToken.UserId, "Session", storedToken.SessionId);
        }
    }

    public async Task SignoutAllAsync(string userId)
    {
        await _refreshTokenRepository.RevokeAllByUserIdAsync(userId);
        await WriteAuditAsync("SignOutAll", AuditOutcome.Success, userId, "User", userId);
    }

    public async Task<IReadOnlyCollection<UserSessionDto>> GetSessionsAsync(string userId, string? currentSessionId)
    {
        var sessions = await _refreshTokenRepository.GetActiveByUserIdAsync(userId);
        return sessions.Select(token => new UserSessionDto
        {
            SessionId = token.SessionId,
            CreatedOn = token.SessionCreatedOn,
            LastSeenOn = token.LastSeenOn,
            ExpiresAt = token.ExpiresAt,
            UserAgent = token.UserAgent,
            IpAddress = token.IpAddress,
            IsCurrent = token.SessionId == currentSessionId
        }).ToList();
    }

    public async Task<RevokeSessionResult> RevokeSessionAsync(string userId, string sessionId, string? currentSessionId)
    {
        var revoked = await _refreshTokenRepository.RevokeSessionAsync(userId, sessionId);
        await WriteAuditAsync("SessionRevoked", revoked ? AuditOutcome.Success : AuditOutcome.Denied, userId, "Session", sessionId, new Dictionary<string, string> { ["CurrentSession"] = (sessionId == currentSessionId).ToString() });
        return new RevokeSessionResult { Revoked = revoked, RevokedCurrentSession = revoked && sessionId == currentSessionId };
    }

    private async Task<AuthSession> CreateSessionAsync(User user, bool keepMeSignedIn, ClientSessionInfo clientSession, string? refreshToken = null, string? refreshTokenHash = null, RefreshToken? previousToken = null)
    {
        refreshToken ??= CreateRefreshToken();
        refreshTokenHash ??= HashToken(refreshToken);

        var now = DateTime.UtcNow;
        var expiresAt = DateTime.UtcNow.AddDays(_refreshTokenSettings.ExpiryDays);
        var sessionId = string.IsNullOrWhiteSpace(previousToken?.SessionId) ? Guid.NewGuid().ToString("N") : previousToken.SessionId;
        var sessionCreatedOn = previousToken is null ? now : previousToken.SessionCreatedOn > DateTime.MinValue ? previousToken.SessionCreatedOn : previousToken.CreatedOn;
        var storedToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = expiresAt,
            CreatedOn = now,
            KeepMeSignedIn = keepMeSignedIn,
            SessionId = sessionId,
            SessionCreatedOn = sessionCreatedOn,
            LastSeenOn = now,
            UserAgent = clientSession.UserAgent,
            IpAddress = clientSession.IpAddress
        };

        await _refreshTokenRepository.CreateAsync(storedToken);

        return new AuthSession
        {
            AccessToken = _tokenService.CreateToken(user, sessionId),
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = expiresAt,
            KeepMeSignedIn = keepMeSignedIn
        };
    }

    private static string CreateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static bool IsRotatedToken(RefreshToken refreshToken)
    {
        return refreshToken.RevokedOn is not null && !string.IsNullOrWhiteSpace(refreshToken.ReplacedByTokenHash);
    }

    private async Task HandleRefreshTokenReuseAsync(string userId)
    {
        await _refreshTokenRepository.RevokeAllByUserIdAsync(userId);
        _logger.LogWarning("Refresh token reuse detected for user {UserId}", userId);
        await WriteAuditAsync("RefreshTokenReuseDetected", AuditOutcome.Denied, userId, "User", userId);
    }

    private Task WriteAuditAsync(string action, AuditOutcome outcome, string? actorUserId = null, string? resourceType = null, string? resourceId = null, IReadOnlyDictionary<string, string>? metadata = null)
    {
        return _auditEventWriter.WriteAsync(new AuditEventRequest { Module = "Authentication", Action = action, Outcome = outcome, ActorUserId = actorUserId, ResourceType = resourceType, ResourceId = resourceId, Metadata = metadata });
    }
}
