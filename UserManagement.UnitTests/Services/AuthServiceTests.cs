using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UserManagement.Application.Interfaces;
using UserManagement.Application.Services;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.UnitTests.Services;

public class AuthServiceTests
{
    private static readonly ClientSessionInfo ClientSession = new() { UserAgent = "Unit test", IpAddress = "127.0.0.1" };
    private readonly Mock<IUserRepository> _repository = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository = new();
    private readonly Mock<IPasswordHasher<User>> _passwordHasher = new();
    private readonly Mock<IJwtTokenService> _tokenService = new();
    private readonly Mock<ICaptchaService> _captchaService = new();
    private readonly Mock<IAuditEventWriter> _auditEventWriter = new();
    private readonly Mock<ILogger<AuthService>> _logger = new();

    private AuthService CreateService()
    {
        return new AuthService(_repository.Object, _refreshTokenRepository.Object, _passwordHasher.Object, _tokenService.Object, Options.Create(new RefreshTokenSettings()), Options.Create(new AccountSecuritySettings { FrontendBaseUrl = "http://localhost:3000" }), Options.Create(new CaptchaSettings { SecretKey = "test-secret", AllowedHostnames = ["localhost"], SigninRequiredAfterFailedAttempts = 2 }), _captchaService.Object, _auditEventWriter.Object, _logger.Object);
    }

    // POST /auth/signin
    [Fact]
    public async Task SigninAsync_IncorrectEmailOrPassword_ReturnsNull()
    {
        var request = new SigninRequest { Email = "wrong@example.com", Password = "wrong-password" };

        _repository.Setup(repository => repository.GetByEmailAsync("wrong@example.com")).ReturnsAsync((User?)null);

        var result = await CreateService().SigninAsync(request, ClientSession);

        Assert.Equal(SigninStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Session);
    }

    // POST /auth/signin
    [Fact]
    public async Task SigninAsync_ValidPassword_ReturnsToken()
    {
        var user = new User { Id = "user-id", Email = "user@example.com", PasswordHash = "stored-hash", Role = "User" };
        var request = new SigninRequest { Email = " USER@EXAMPLE.COM ", Password = "password123" };
        var expectedResponse = new AuthResponse { Token = "jwt-token", ExpiresAt = DateTime.UtcNow.AddHours(1) };

        _repository.Setup(repository => repository.GetByEmailAsync("user@example.com")).ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyHashedPassword(user, "stored-hash", "password123")).Returns(PasswordVerificationResult.Success);
        _tokenService.Setup(service => service.CreateToken(user, It.IsAny<string>())).Returns(expectedResponse);

        var result = await CreateService().SigninAsync(request, ClientSession);

        Assert.Equal(SigninStatus.Success, result.Status);
        Assert.NotNull(result.Session);
        Assert.Same(expectedResponse, result.Session.AccessToken);
        Assert.False(string.IsNullOrWhiteSpace(result.Session.RefreshToken));
        Assert.False(result.Session.KeepMeSignedIn);
        _repository.Verify(repository => repository.SetLastSignInOnAsync(user.Id, It.Is<DateTime>(value => value <= DateTime.UtcNow && value >= DateTime.UtcNow.AddMinutes(-1))), Times.Once);
        _refreshTokenRepository.Verify(repository => repository.CreateAsync(It.IsAny<RefreshToken>()), Times.Once);
        _auditEventWriter.Verify(writer => writer.WriteAsync(It.Is<AuditEventRequest>(audit => audit.Module == "Authentication" && audit.Action == "SignIn" && audit.Outcome == AuditOutcome.Success && audit.ActorUserId == user.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SigninAsync_UnverifiedEmail_ReturnsVerificationRequired()
    {
        var user = new User { Id = "user-id", Email = "user@example.com", PasswordHash = "stored-hash", IsEmailVerified = false };
        _repository.Setup(repository => repository.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyHashedPassword(user, user.PasswordHash, "password123")).Returns(PasswordVerificationResult.Success);

        var result = await CreateService().SigninAsync(new SigninRequest { Email = user.Email, Password = "password123" }, ClientSession);

        Assert.Equal(SigninStatus.EmailVerificationRequired, result.Status);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task SigninAsync_DisabledAccount_ReturnsAccountDisabled()
    {
        var user = new User { Id = "user-id", Email = "user@example.com", PasswordHash = "stored-hash", Status = AccountStatus.Disabled };
        _repository.Setup(repository => repository.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyHashedPassword(user, user.PasswordHash, "password123")).Returns(PasswordVerificationResult.Success);

        var result = await CreateService().SigninAsync(new SigninRequest { Email = user.Email, Password = "password123" }, ClientSession);

        Assert.Equal(SigninStatus.AccountDisabled, result.Status);
        Assert.Null(result.Session);
        _refreshTokenRepository.Verify(repository => repository.CreateAsync(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task SigninAsync_MaximumFailures_LocksAccount()
    {
        var user = new User { Id = "user-id", Email = "user@example.com", PasswordHash = "stored-hash", FailedSigninAttempts = 4 };
        _repository.Setup(repository => repository.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _repository.Setup(repository => repository.IncrementFailedSigninAttemptsAsync(user.Id)).ReturnsAsync(new User { Id = user.Id, FailedSigninAttempts = 5 });
        _passwordHasher.Setup(hasher => hasher.VerifyHashedPassword(user, user.PasswordHash, "wrong-password")).Returns(PasswordVerificationResult.Failed);
        _captchaService.Setup(service => service.ValidateAsync("captcha-token", "signin", ClientSession.IpAddress, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateService().SigninAsync(new SigninRequest { Email = user.Email, Password = "wrong-password", CaptchaToken = "captcha-token" }, ClientSession);

        Assert.Equal(SigninStatus.LockedOut, result.Status);
        _repository.Verify(repository => repository.SetLockoutEndAsync(user.Id, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task SigninAsync_SecondFailure_RequiresCaptchaForNextAttempt()
    {
        var user = new User { Id = "user-id", Email = "user@example.com", PasswordHash = "stored-hash", FailedSigninAttempts = 1 };
        _repository.Setup(repository => repository.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _repository.Setup(repository => repository.IncrementFailedSigninAttemptsAsync(user.Id)).ReturnsAsync(new User { Id = user.Id, FailedSigninAttempts = 2 });
        _passwordHasher.Setup(hasher => hasher.VerifyHashedPassword(user, user.PasswordHash, "wrong-password")).Returns(PasswordVerificationResult.Failed);

        var result = await CreateService().SigninAsync(new SigninRequest { Email = user.Email, Password = "wrong-password" }, ClientSession);

        Assert.Equal(SigninStatus.InvalidCredentials, result.Status);
        Assert.True(result.CaptchaRequired);
    }

    [Fact]
    public async Task SigninAsync_RepeatedFailuresWithoutValidCaptcha_DoesNotCheckPassword()
    {
        var user = new User { Id = "user-id", Email = "user@example.com", PasswordHash = "stored-hash", FailedSigninAttempts = 2 };
        _repository.Setup(repository => repository.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _captchaService.Setup(service => service.ValidateAsync(null, "signin", ClientSession.IpAddress, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateService().SigninAsync(new SigninRequest { Email = user.Email, Password = "password123" }, ClientSession);

        Assert.Equal(SigninStatus.CaptchaRequired, result.Status);
        Assert.True(result.CaptchaRequired);
        _passwordHasher.Verify(hasher => hasher.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_ReturnsNull()
    {
        var storedToken = new RefreshToken { Id = "refresh-id", UserId = "user-id", ExpiresAt = DateTime.UtcNow.AddDays(-1) };

        _refreshTokenRepository.Setup(repository => repository.GetByTokenHashAsync(It.IsAny<string>())).ReturnsAsync(storedToken);

        var result = await CreateService().RefreshAsync("expired-refresh-token", ClientSession);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshAsync_RevokedToken_ReturnsNull()
    {
        var storedToken = new RefreshToken { Id = "refresh-id", UserId = "user-id", ExpiresAt = DateTime.UtcNow.AddDays(7), RevokedOn = DateTime.UtcNow };

        _refreshTokenRepository.Setup(repository => repository.GetByTokenHashAsync(It.IsAny<string>())).ReturnsAsync(storedToken);

        var result = await CreateService().RefreshAsync("reused-refresh-token", ClientSession);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshAsync_RotatedToken_RevokesAllUserSessionsAndReturnsNull()
    {
        var storedToken = new RefreshToken
        {
            Id = "refresh-id",
            UserId = "user-id",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedOn = DateTime.UtcNow,
            ReplacedByTokenHash = "replacement-token-hash"
        };

        _refreshTokenRepository.Setup(repository => repository.GetByTokenHashAsync(It.IsAny<string>())).ReturnsAsync(storedToken);

        var result = await CreateService().RefreshAsync("reused-refresh-token", ClientSession);

        Assert.Null(result);
        _refreshTokenRepository.Verify(repository => repository.RevokeAllByUserIdAsync("user-id"), Times.Once);
        _auditEventWriter.Verify(writer => writer.WriteAsync(It.Is<AuditEventRequest>(audit => audit.Action == "RefreshTokenReuseDetected" && audit.Outcome == AuditOutcome.Denied), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsNewSession()
    {
        var createdOn = DateTime.UtcNow.AddDays(-1);
        var storedToken = new RefreshToken { Id = "refresh-id", UserId = "user-id", ExpiresAt = DateTime.UtcNow.AddDays(7), KeepMeSignedIn = true, SessionId = "session-id", SessionCreatedOn = createdOn, CreatedOn = createdOn };
        var user = new User { Id = "user-id", Email = "user@example.com", Role = "User" };
        var expectedResponse = new AuthResponse { Token = "new-jwt-token", ExpiresAt = DateTime.UtcNow.AddHours(1) };

        _refreshTokenRepository.Setup(repository => repository.GetByTokenHashAsync(It.IsAny<string>())).ReturnsAsync(storedToken);
        _refreshTokenRepository.Setup(repository => repository.TryRevokeAsync("refresh-id", It.IsAny<string>())).ReturnsAsync(true);
        _repository.Setup(repository => repository.GetByIdAsync("user-id")).ReturnsAsync(user);
        _tokenService.Setup(service => service.CreateToken(user, "session-id")).Returns(expectedResponse);

        var result = await CreateService().RefreshAsync("valid-refresh-token", ClientSession);

        Assert.NotNull(result);
        Assert.Same(expectedResponse, result.AccessToken);
        Assert.True(result.KeepMeSignedIn);
        _refreshTokenRepository.Verify(repository => repository.CreateAsync(It.Is<RefreshToken>(token => token.SessionId == "session-id" && token.SessionCreatedOn == createdOn && token.UserAgent == ClientSession.UserAgent && token.IpAddress == ClientSession.IpAddress)), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_DisabledAccount_RevokesTokenAndReturnsNull()
    {
        var storedToken = new RefreshToken { Id = "refresh-id", UserId = "user-id", ExpiresAt = DateTime.UtcNow.AddDays(7) };
        _refreshTokenRepository.Setup(repository => repository.GetByTokenHashAsync(It.IsAny<string>())).ReturnsAsync(storedToken);
        _repository.Setup(repository => repository.GetByIdAsync("user-id")).ReturnsAsync(new User { Id = "user-id", Status = AccountStatus.Disabled });

        var result = await CreateService().RefreshAsync("valid-refresh-token", ClientSession);

        Assert.Null(result);
        _refreshTokenRepository.Verify(repository => repository.TryRevokeAsync("refresh-id", null), Times.Once);
    }

    [Fact]
    public async Task GetSessionsAsync_MarksCurrentSession()
    {
        _refreshTokenRepository.Setup(repository => repository.GetActiveByUserIdAsync("user-id")).ReturnsAsync([new RefreshToken { SessionId = "current-session", UserId = "user-id" }, new RefreshToken { SessionId = "other-session", UserId = "user-id" }]);

        var result = await CreateService().GetSessionsAsync("user-id", "current-session");

        Assert.True(result.Single(session => session.SessionId == "current-session").IsCurrent);
        Assert.False(result.Single(session => session.SessionId == "other-session").IsCurrent);
    }

    [Fact]
    public async Task RevokeSessionAsync_CurrentOwnedSession_ReturnsCurrentSessionRevoked()
    {
        _refreshTokenRepository.Setup(repository => repository.RevokeSessionAsync("user-id", "session-id")).ReturnsAsync(true);

        var result = await CreateService().RevokeSessionAsync("user-id", "session-id", "session-id");

        Assert.True(result.Revoked);
        Assert.True(result.RevokedCurrentSession);
    }

    [Fact]
    public async Task RevokeSessionAsync_UnownedSession_ReturnsNotRevoked()
    {
        _refreshTokenRepository.Setup(repository => repository.RevokeSessionAsync("user-id", "other-session")).ReturnsAsync(false);

        var result = await CreateService().RevokeSessionAsync("user-id", "other-session", "current-session");

        Assert.False(result.Revoked);
        Assert.False(result.RevokedCurrentSession);
    }

    [Fact]
    public async Task SignoutAsync_ValidToken_RevokesToken()
    {
        var storedToken = new RefreshToken { Id = "refresh-id", UserId = "user-id", ExpiresAt = DateTime.UtcNow.AddDays(7) };

        _refreshTokenRepository.Setup(repository => repository.GetByTokenHashAsync(It.IsAny<string>())).ReturnsAsync(storedToken);
        _refreshTokenRepository.Setup(repository => repository.TryRevokeAsync("refresh-id", null)).ReturnsAsync(true);

        await CreateService().SignoutAsync("valid-refresh-token");

        _refreshTokenRepository.Verify(repository => repository.TryRevokeAsync("refresh-id", null), Times.Once);
    }

    [Fact]
    public async Task SignoutAllAsync_RevokesAllUserSessions()
    {
        await CreateService().SignoutAllAsync("user-id");

        _refreshTokenRepository.Verify(repository => repository.RevokeAllByUserIdAsync("user-id"), Times.Once);
    }
}
