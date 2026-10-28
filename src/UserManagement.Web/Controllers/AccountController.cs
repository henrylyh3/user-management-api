using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Settings;

namespace UserManagement.Web.Controllers;

[ApiController]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly ICurrentUserService _currentUser;
    private readonly ICaptchaService _captchaService;
    private readonly IAntiforgery _antiforgery;
    private readonly RefreshTokenSettings _refreshTokenSettings;
    private readonly ILogger<AccountController> _logger;
    private readonly bool _useSecureCookies;

    public AccountController(IAccountService accountService, ICurrentUserService currentUser, ICaptchaService captchaService, IAntiforgery antiforgery, IOptions<RefreshTokenSettings> refreshTokenOptions, ILogger<AccountController> logger, IWebHostEnvironment environment)
    {
        _accountService = accountService;
        _currentUser = currentUser;
        _captchaService = captchaService;
        _antiforgery = antiforgery;
        _refreshTokenSettings = refreshTokenOptions.Value;
        _logger = logger;
        _useSecureCookies = !environment.IsDevelopment();
    }

    [AllowAnonymous]
    [HttpPost("~/api/auth/signup")]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> Signup(SignupRequest request, CancellationToken cancellationToken)
    {
        if (!await _accountService.SignupAsync(request, cancellationToken))
        {
            await _accountService.RequestEmailVerificationAsync(request.Email, cancellationToken);
        }

        return Accepted(new { message = "Account created. Check your email to verify your account." });
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!await _captchaService.ValidateAsync(request.CaptchaToken, "forgot_password", HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Human verification failed", detail: "Complete the human verification and try again.", extensions: new Dictionary<string, object?> { ["code"] = "captcha_failed" });
        }

        await _accountService.RequestPasswordResetAsync(request.Email, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!await _accountService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid reset link", detail: "The password reset link is invalid or expired.");
        }

        DeleteRefreshTokenCookie();
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("verify-email")]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> VerifyEmail(AccountTokenRequest request, CancellationToken cancellationToken)
    {
        if (!await _accountService.VerifyEmailAsync(request.Token, cancellationToken))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid verification link", detail: "The email verification link is invalid or expired.");
        }

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("resend-verification")]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> ResendVerification(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _accountService.RequestEmailVerificationAsync(request.Email, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!await IsCsrfRequestValidAsync())
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid CSRF token", detail: "The request could not be verified.");
        }

        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthenticated", detail: "Please sign in again.");
        }

        if (!await _accountService.ChangePasswordAsync(_currentUser.UserId, request.CurrentPassword, request.NewPassword, cancellationToken))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Password not changed", detail: "The current password is incorrect or the new password matches it.");
        }

        DeleteRefreshTokenCookie();
        _logger.LogInformation("Password changed and sessions revoked for user {UserId}", _currentUser.UserId);
        return NoContent();
    }

    [Authorize]
    [HttpGet("export")]
    public async Task<IActionResult> ExportPersonalData(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthenticated", detail: "Please sign in again.");
        }

        var export = await _accountService.ExportPersonalDataAsync(_currentUser.UserId, cancellationToken);
        return export is null ? NotFound() : Ok(export);
    }

    [Authorize]
    [HttpDelete]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        if (!await IsCsrfRequestValidAsync())
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid CSRF token", detail: "The request could not be verified.");
        }

        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthenticated", detail: "Please sign in again.");
        }

        if (!await _accountService.DeleteAccountAsync(_currentUser.UserId, request.CurrentPassword, cancellationToken))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Account not deleted", detail: "The current password is incorrect.");
        }

        DeleteRefreshTokenCookie();
        return NoContent();
    }

    private async Task<bool> IsCsrfRequestValidAsync()
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete(_refreshTokenSettings.CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = _useSecureCookies,
            SameSite = _useSecureCookies ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/api/auth"
        });
    }
}
