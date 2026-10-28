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

public sealed class AccountServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IAccountTokenRepository> _accountTokens = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IPasswordHasher<User>> _passwordHasher = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly Mock<IAuditEventWriter> _auditEventWriter = new();

    private AccountService CreateService()
    {
        var settings = new AccountSecuritySettings { FrontendBaseUrl = "http://localhost:3000" };
        return new AccountService(_users.Object, _accountTokens.Object, _refreshTokens.Object, _passwordHasher.Object, _email.Object, _fileStorage.Object, _auditEventWriter.Object, Options.Create(settings), Mock.Of<ILogger<AccountService>>());
    }

    [Fact]
    public async Task SignupAsync_NewUser_CreatesUnverifiedUserAndSendsVerificationEmail()
    {
        var request = new SignupRequest { FirstName = " Henry ", LastName = " Lee ", Email = " HENRY@EXAMPLE.COM ", Password = "password123" };
        _users.Setup(repository => repository.GetByEmailAsync("henry@example.com")).ReturnsAsync((User?)null);
        _users.Setup(repository => repository.CreateAsync(It.IsAny<User>())).Callback<User>(user => user.Id = "user-id").Returns(Task.CompletedTask);
        _users.Setup(repository => repository.TryAcquireAccountEmailCooldownAsync("user-id", AccountTokenPurpose.EmailVerification, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _passwordHasher.Setup(hasher => hasher.HashPassword(It.IsAny<User>(), request.Password)).Returns("password-hash");

        var result = await CreateService().SignupAsync(request);

        Assert.True(result);
        _users.Verify(repository => repository.CreateAsync(It.Is<User>(user => user.Email == "henry@example.com" && !user.IsEmailVerified && user.PasswordHash == "password-hash")), Times.Once);
        _accountTokens.Verify(repository => repository.CreateAsync(It.Is<AccountToken>(token => token.Purpose == AccountTokenPurpose.EmailVerification), It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(service => service.SendAsync("henry@example.com", "Verify your email", It.Is<string>(body => body.Contains("/verify-email?token=", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_UnknownEmail_DoesNotSendEmail()
    {
        _users.Setup(repository => repository.GetByEmailAsync("missing@example.com")).ReturnsAsync((User?)null);

        await CreateService().RequestPasswordResetAsync("missing@example.com");

        _email.Verify(service => service.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_CooldownActive_DoesNotCreateTokenOrSendEmail()
    {
        var user = new User { Id = "user-id", Email = "henry@example.com" };
        _users.Setup(repository => repository.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _users.Setup(repository => repository.TryAcquireAccountEmailCooldownAsync(user.Id, AccountTokenPurpose.PasswordReset, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await CreateService().RequestPasswordResetAsync(user.Email);

        _accountTokens.Verify(repository => repository.CreateAsync(It.IsAny<AccountToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _email.Verify(service => service.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_ChangesPasswordAndRevokesSessions()
    {
        var token = new AccountToken { Id = "token-id", UserId = "user-id", Purpose = AccountTokenPurpose.PasswordReset, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        var user = new User { Id = "user-id", PasswordHash = "old-hash" };
        _accountTokens.Setup(repository => repository.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _accountTokens.Setup(repository => repository.TryConsumeAsync(token.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _users.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyHashedPassword(user, user.PasswordHash, "new-password")).Returns(PasswordVerificationResult.Failed);
        _passwordHasher.Setup(hasher => hasher.HashPassword(user, "new-password")).Returns("new-hash");
        _users.Setup(repository => repository.UpdatePasswordHashAndIncrementTokenVersionAsync(user.Id, "new-hash")).ReturnsAsync(true);

        var result = await CreateService().ResetPasswordAsync("raw-token", "new-password");

        Assert.True(result);
        _refreshTokens.Verify(repository => repository.RevokeAllByUserIdAsync(user.Id), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_ConsumedToken_ReturnsFalse()
    {
        var token = new AccountToken { Id = "token-id", UserId = "user-id", Purpose = AccountTokenPurpose.PasswordReset, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        var user = new User { Id = "user-id", PasswordHash = "old-hash" };
        _accountTokens.Setup(repository => repository.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _accountTokens.Setup(repository => repository.TryConsumeAsync(token.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyHashedPassword(user, user.PasswordHash, "new-password")).Returns(PasswordVerificationResult.Failed);
        _passwordHasher.Setup(hasher => hasher.HashPassword(user, "new-password")).Returns("new-hash");

        var result = await CreateService().ResetPasswordAsync("raw-token", "new-password");

        Assert.False(result);
        _users.Verify(repository => repository.UpdatePasswordHashAndIncrementTokenVersionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidCurrentPassword_ChangesPasswordAndRevokesSessions()
    {
        var user = new User { Id = "user-id", PasswordHash = "old-hash" };
        _users.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyHashedPassword(user, user.PasswordHash, "current-password")).Returns(PasswordVerificationResult.Success);
        _passwordHasher.Setup(hasher => hasher.VerifyHashedPassword(user, user.PasswordHash, "new-password")).Returns(PasswordVerificationResult.Failed);
        _passwordHasher.Setup(hasher => hasher.HashPassword(user, "new-password")).Returns("new-hash");
        _users.Setup(repository => repository.UpdatePasswordHashAndIncrementTokenVersionAsync(user.Id, "new-hash")).ReturnsAsync(true);

        var result = await CreateService().ChangePasswordAsync(user.Id, "current-password", "new-password");

        Assert.True(result);
        _refreshTokens.Verify(repository => repository.RevokeAllByUserIdAsync(user.Id), Times.Once);
    }

    [Fact]
    public async Task ExportPersonalDataAsync_ExistingUser_ReturnsProfileAndSessions()
    {
        var user = new User { Id = "user-id", FirstName = "Henry", LastName = "Lee", Email = "henry@example.com", Role = "User", ImageUrl = "profiles/image.jpg" };
        _users.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _refreshTokens.Setup(repository => repository.GetActiveByUserIdAsync(user.Id)).ReturnsAsync([new RefreshToken { SessionId = "session-id", UserId = user.Id }]);
        _fileStorage.Setup(service => service.GetUrl(user.ImageUrl)).Returns("https://files.example.com/profiles/image.jpg");

        var result = await CreateService().ExportPersonalDataAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal("https://files.example.com/profiles/image.jpg", result.ImageUrl);
        Assert.Single(result.ActiveSessions);
    }

    [Fact]
    public async Task DeleteAccountAsync_IncorrectPassword_DoesNotDeleteData()
    {
        var user = new User { Id = "user-id", PasswordHash = "stored-hash", ImageUrl = "profiles/image.jpg" };
        _users.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyHashedPassword(user, user.PasswordHash, "wrong-password")).Returns(PasswordVerificationResult.Failed);

        var result = await CreateService().DeleteAccountAsync(user.Id, "wrong-password");

        Assert.False(result);
        _fileStorage.Verify(service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(repository => repository.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAccountAsync_ValidPassword_DeletesOwnedData()
    {
        var user = new User { Id = "user-id", PasswordHash = "stored-hash", ImageUrl = "profiles/image.jpg" };
        _users.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyHashedPassword(user, user.PasswordHash, "current-password")).Returns(PasswordVerificationResult.Success);
        _users.Setup(repository => repository.DeleteAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateService().DeleteAccountAsync(user.Id, "current-password");

        Assert.True(result);
        _fileStorage.Verify(service => service.DeleteAsync(user.ImageUrl, It.IsAny<CancellationToken>()), Times.Once);
        _accountTokens.Verify(repository => repository.DeleteByUserIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokens.Verify(repository => repository.DeleteByUserIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(repository => repository.DeleteAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        _auditEventWriter.Verify(writer => writer.WriteAsync(It.Is<AuditEventRequest>(audit => audit.Module == "Account" && audit.Action == "AccountDeletion" && audit.Outcome == AuditOutcome.Success && audit.ActorUserId == user.Id), It.IsAny<CancellationToken>()), Times.Once);
    }
}
