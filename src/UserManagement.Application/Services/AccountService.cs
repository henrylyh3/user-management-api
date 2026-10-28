using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.Application.Services;

public sealed class AccountService : IAccountService
{
    private readonly IUserRepository _userRepository;
    private readonly IAccountTokenRepository _accountTokenRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly AccountSecuritySettings _settings;
    private readonly ILogger<AccountService> _logger;

    public AccountService(IUserRepository userRepository, IAccountTokenRepository accountTokenRepository, IRefreshTokenRepository refreshTokenRepository, IPasswordHasher<User> passwordHasher, IEmailService emailService, IFileStorageService fileStorageService, IAuditEventWriter auditEventWriter, IOptions<AccountSecuritySettings> options, ILogger<AccountService> logger)
    {
        _userRepository = userRepository;
        _accountTokenRepository = accountTokenRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _fileStorageService = fileStorageService;
        _auditEventWriter = auditEventWriter;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<bool> SignupAsync(SignupRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        if (await _userRepository.GetByEmailAsync(email) is not null)
        {
            return false;
        }

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            Role = UserRoles.User,
            IsEmailVerified = false
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        await _userRepository.CreateAsync(user);
        await SendEmailVerificationAsync(user, cancellationToken);
        await WriteAuditAsync("AccountCreated", AuditOutcome.Success, user.Id, "User", user.Id, cancellationToken);
        return true;
    }

    public async Task RequestEmailVerificationAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(NormalizeEmail(email));

        if (user is null || user.IsEmailVerified)
        {
            return;
        }

        await SendEmailVerificationAsync(user, cancellationToken);
    }

    public async Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        var accountToken = await GetValidTokenAsync(token, AccountTokenPurpose.EmailVerification, cancellationToken);

        if (accountToken is null || !await _accountTokenRepository.TryConsumeAsync(accountToken.Id, DateTime.UtcNow, cancellationToken))
        {
            await WriteAuditAsync("EmailVerification", AuditOutcome.Failure, cancellationToken: cancellationToken);
            return false;
        }

        await _userRepository.SetEmailVerifiedAsync(accountToken.UserId);
        _logger.LogInformation("Email verified for user {UserId}", accountToken.UserId);
        await WriteAuditAsync("EmailVerification", AuditOutcome.Success, accountToken.UserId, "User", accountToken.UserId, cancellationToken);
        return true;
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(NormalizeEmail(email));

        if (user is null)
        {
            return;
        }

        if (!await TryAcquireEmailCooldownAsync(user.Id, AccountTokenPurpose.PasswordReset, cancellationToken))
        {
            _logger.LogInformation("Password reset email suppressed by cooldown for user {UserId}", user.Id);
            return;
        }

        var (rawToken, accountToken) = CreateAccountToken(user.Id, AccountTokenPurpose.PasswordReset, _settings.PasswordResetTokenExpiryMinutes);
        await _accountTokenRepository.RevokeActiveByUserIdAndPurposeAsync(user.Id, AccountTokenPurpose.PasswordReset, DateTime.UtcNow, cancellationToken);
        await _accountTokenRepository.CreateAsync(accountToken, cancellationToken);
        var link = CreateFrontendLink("reset-password", rawToken);
        await TrySendEmailAsync(user.Id, user.Email, "Reset your password", CreateLinkEmail("Reset your password", "Reset password", link, _settings.PasswordResetTokenExpiryMinutes), cancellationToken);
        await WriteAuditAsync("PasswordResetRequested", AuditOutcome.Success, user.Id, "User", user.Id, cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var accountToken = await GetValidTokenAsync(token, AccountTokenPurpose.PasswordReset, cancellationToken);

        if (accountToken is null)
        {
            await WriteAuditAsync("PasswordReset", AuditOutcome.Failure, cancellationToken: cancellationToken);
            return false;
        }

        var user = await _userRepository.GetByIdAsync(accountToken.UserId);

        if (user is null)
        {
            await WriteAuditAsync("PasswordReset", AuditOutcome.Failure, accountToken.UserId, "User", accountToken.UserId, cancellationToken);
            return false;
        }

        if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, newPassword) != PasswordVerificationResult.Failed)
        {
            await WriteAuditAsync("PasswordReset", AuditOutcome.Failure, user.Id, "User", user.Id, cancellationToken);
            return false;
        }

        var passwordHash = _passwordHasher.HashPassword(user, newPassword);

        if (!await _accountTokenRepository.TryConsumeAsync(accountToken.Id, DateTime.UtcNow, cancellationToken))
        {
            await WriteAuditAsync("PasswordReset", AuditOutcome.Failure, user.Id, "User", user.Id, cancellationToken);
            return false;
        }

        if (!await _userRepository.UpdatePasswordHashAndIncrementTokenVersionAsync(user.Id, passwordHash))
        {
            await WriteAuditAsync("PasswordReset", AuditOutcome.Failure, user.Id, "User", user.Id, cancellationToken);
            return false;
        }

        await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id);
        _logger.LogInformation("Password reset completed and sessions revoked for user {UserId}", user.Id);
        await WriteAuditAsync("PasswordReset", AuditOutcome.Success, user.Id, "User", user.Id, cancellationToken);
        return true;
    }

    public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            await WriteAuditAsync("PasswordChange", AuditOutcome.Failure, userId, "User", userId, cancellationToken);
            return false;
        }

        if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, newPassword) != PasswordVerificationResult.Failed)
        {
            await WriteAuditAsync("PasswordChange", AuditOutcome.Failure, userId, "User", userId, cancellationToken);
            return false;
        }

        var passwordHash = _passwordHasher.HashPassword(user, newPassword);

        if (!await _userRepository.UpdatePasswordHashAndIncrementTokenVersionAsync(user.Id, passwordHash))
        {
            await WriteAuditAsync("PasswordChange", AuditOutcome.Failure, userId, "User", userId, cancellationToken);
            return false;
        }

        await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id);
        await WriteAuditAsync("PasswordChange", AuditOutcome.Success, user.Id, "User", user.Id, cancellationToken);
        return true;
    }

    public async Task<PersonalDataExportDto?> ExportPersonalDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null)
        {
            return null;
        }

        var sessions = await _refreshTokenRepository.GetActiveByUserIdAsync(userId);
        var export = new PersonalDataExportDto
        {
            ExportedOn = DateTime.UtcNow,
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role,
            Status = user.Status.ToString(),
            DisabledOn = user.DisabledOn,
            ImageUrl = string.IsNullOrWhiteSpace(user.ImageUrl) ? null : _fileStorageService.GetUrl(user.ImageUrl),
            IsEmailVerified = user.IsEmailVerified,
            CreatedOn = user.CreatedOn,
            ModifiedOn = user.ModifiedOn,
            CreatedBy = user.CreatedBy,
            ModifiedBy = user.ModifiedBy,
            ActiveSessions = sessions.Select(token => new UserSessionDto
            {
                SessionId = token.SessionId,
                CreatedOn = token.SessionCreatedOn,
                LastSeenOn = token.LastSeenOn,
                ExpiresAt = token.ExpiresAt,
                UserAgent = token.UserAgent,
                IpAddress = token.IpAddress
            }).ToList()
        };
        await WriteAuditAsync("PersonalDataExported", AuditOutcome.Success, user.Id, "User", user.Id, cancellationToken);
        return export;
    }

    public async Task<bool> DeleteAccountAsync(string userId, string currentPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            await WriteAuditAsync("AccountDeletion", AuditOutcome.Failure, userId, "User", userId, cancellationToken);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(user.ImageUrl))
        {
            await _fileStorageService.DeleteAsync(user.ImageUrl, cancellationToken);
        }

        await _accountTokenRepository.DeleteByUserIdAsync(userId, cancellationToken);
        await _refreshTokenRepository.DeleteByUserIdAsync(userId, cancellationToken);

        if (!await _userRepository.DeleteAsync(userId, cancellationToken))
        {
            throw new InvalidOperationException("The account could not be deleted.");
        }

        _logger.LogInformation("Account deleted for user {UserId}", userId);
        await WriteAuditAsync("AccountDeletion", AuditOutcome.Success, userId, "User", userId, cancellationToken);
        return true;
    }

    private async Task SendEmailVerificationAsync(User user, CancellationToken cancellationToken)
    {
        if (!await TryAcquireEmailCooldownAsync(user.Id, AccountTokenPurpose.EmailVerification, cancellationToken))
        {
            _logger.LogInformation("Email verification suppressed by cooldown for user {UserId}", user.Id);
            return;
        }

        var (rawToken, accountToken) = CreateAccountToken(user.Id, AccountTokenPurpose.EmailVerification, _settings.EmailVerificationTokenExpiryMinutes);
        await _accountTokenRepository.RevokeActiveByUserIdAndPurposeAsync(user.Id, AccountTokenPurpose.EmailVerification, DateTime.UtcNow, cancellationToken);
        await _accountTokenRepository.CreateAsync(accountToken, cancellationToken);
        var link = CreateFrontendLink("verify-email", rawToken);
        await TrySendEmailAsync(user.Id, user.Email, "Verify your email", CreateLinkEmail("Verify your email", "Verify email", link, _settings.EmailVerificationTokenExpiryMinutes), cancellationToken);
        await WriteAuditAsync("EmailVerificationRequested", AuditOutcome.Success, user.Id, "User", user.Id, cancellationToken);
    }

    private Task<bool> TryAcquireEmailCooldownAsync(string userId, AccountTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return _userRepository.TryAcquireAccountEmailCooldownAsync(userId, purpose, now, now.AddSeconds(_settings.AccountEmailCooldownSeconds), cancellationToken);
    }

    private async Task<AccountToken?> GetValidTokenAsync(string rawToken, AccountTokenPurpose purpose, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var accountToken = await _accountTokenRepository.GetByTokenHashAsync(HashToken(rawToken), cancellationToken);
        return accountToken is not null && accountToken.Purpose == purpose && accountToken.ConsumedOn is null && accountToken.ExpiresAt > DateTime.UtcNow ? accountToken : null;
    }

    private async Task TrySendEmailAsync(string userId, string email, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(email, subject, htmlBody, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Account email delivery failed for user {UserId}", userId);
        }
    }

    private (string RawToken, AccountToken StoredToken) CreateAccountToken(string userId, AccountTokenPurpose purpose, int expiryMinutes)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return (rawToken, new AccountToken { UserId = userId, TokenHash = HashToken(rawToken), Purpose = purpose, ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes) });
    }

    private string CreateFrontendLink(string path, string rawToken)
    {
        return $"{_settings.FrontendBaseUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(rawToken)}";
    }

    private static string CreateLinkEmail(string heading, string action, string link, int expiryMinutes)
    {
        var safeHeading = WebUtility.HtmlEncode(heading);
        var safeAction = WebUtility.HtmlEncode(action);
        var safeLink = WebUtility.HtmlEncode(link);
        return $"<h1>{safeHeading}</h1><p><a href=\"{safeLink}\">{safeAction}</a></p><p>This link expires in {expiryMinutes} minutes and can only be used once.</p>";
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private Task WriteAuditAsync(string action, AuditOutcome outcome, string? actorUserId = null, string? resourceType = null, string? resourceId = null, CancellationToken cancellationToken = default)
    {
        return _auditEventWriter.WriteAsync(new AuditEventRequest { Module = "Account", Action = action, Outcome = outcome, ActorUserId = actorUserId, ResourceType = resourceType, ResourceId = resourceId }, cancellationToken);
    }
}
