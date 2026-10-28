using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Serilog;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Settings;

namespace UserManagement.Web.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuthController> _logger;
    private readonly IAntiforgery _antiforgery;
    private readonly RefreshTokenSettings _refreshTokenSettings;
    private readonly bool _useSecureCookies;

    public AuthController(
        IAuthService authService,
        ICurrentUserService currentUser,
        ILogger<AuthController> logger,
        IAntiforgery antiforgery,
        IOptions<RefreshTokenSettings> refreshTokenOptions,
        IWebHostEnvironment environment
        )
    {
        _authService = authService;
        _currentUser = currentUser;
        _logger = logger;
        _antiforgery = antiforgery;
        _refreshTokenSettings = refreshTokenOptions.Value;
        _useSecureCookies = !environment.IsDevelopment();
    }

    [AllowAnonymous]
    [HttpPost("signin")]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> Signin(SigninRequest request)
    {
        var result = await _authService.SigninAsync(request, CreateClientSessionInfo(), HttpContext.RequestAborted);

        if (result.Status == SigninStatus.InvalidCredentials)
        {
            _logger.LogWarning("Signin rejected because the credentials were invalid");
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials", detail: "Invalid email or password.", extensions: new Dictionary<string, object?> { ["code"] = "invalid_credentials", ["captchaRequired"] = result.CaptchaRequired });
        }

        if (result.Status == SigninStatus.CaptchaRequired)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Human verification failed", detail: "Complete the human verification and try again.", extensions: new Dictionary<string, object?> { ["code"] = "captcha_required", ["captchaRequired"] = true });
        }

        if (result.Status == SigninStatus.EmailVerificationRequired)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Email verification required", detail: "Verify your email before signing in.");
        }

        if (result.Status == SigninStatus.LockedOut)
        {
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Account temporarily locked", detail: "Too many failed sign-in attempts. Try again later.");
        }

        if (result.Status == SigninStatus.AccountDisabled)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Account unavailable", detail: "This account is unavailable. Contact an administrator.", extensions: new Dictionary<string, object?> { ["code"] = "account_disabled" });
        }

        var session = result.Session ?? throw new InvalidOperationException("Successful sign-in did not create a session.");

        SetRefreshTokenCookie(session);
        _logger.LogInformation("User signin completed");
        return Ok(session.AccessToken);
    }

    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

        return Ok(new { token = tokens.RequestToken });
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> Refresh()
    {
        if (!await IsCsrfRequestValidAsync())
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid CSRF token", detail: "The request could not be verified.");
        }

        Request.Cookies.TryGetValue(_refreshTokenSettings.CookieName, out var refreshToken);
        var session = await _authService.RefreshAsync(refreshToken ?? string.Empty, CreateClientSessionInfo());

        if (session is null)
        {
            DeleteRefreshTokenCookie();
            _logger.LogWarning("Refresh token rejected");
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Session expired", detail: "Please sign in again.");
        }

        SetRefreshTokenCookie(session);
        return Ok(session.AccessToken);
    }

    [AllowAnonymous]
    [HttpPost("signout")]
    public async Task<IActionResult> Signout()
    {
        if (!await IsCsrfRequestValidAsync())
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid CSRF token", detail: "The request could not be verified.");
        }

        Request.Cookies.TryGetValue(_refreshTokenSettings.CookieName, out var refreshToken);
        await _authService.SignoutAsync(refreshToken ?? string.Empty);
        DeleteRefreshTokenCookie();
        _logger.LogInformation("User signed out");

        return NoContent();
    }

    [Authorize]
    [HttpPost("signout-all")]
    public async Task<IActionResult> SignoutAll()
    {
        if (!await IsCsrfRequestValidAsync())
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid CSRF token", detail: "The request could not be verified.");
        }

        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthenticated", detail: "Please sign in again.");
        }

        await _authService.SignoutAllAsync(_currentUser.UserId);
        DeleteRefreshTokenCookie();
        _logger.LogInformation("All refresh sessions were revoked");

        return NoContent();
    }

    [Authorize]
    [HttpGet("whoami")]
    public IActionResult GetTokenInfo()
    {
        return Ok(new
        {
            _currentUser.IsAuthenticated,
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.Role,
            _currentUser.IsAdmin
        });
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthenticated", detail: "Please sign in again.");
        }

        return Ok(await _authService.GetSessionsAsync(_currentUser.UserId, _currentUser.SessionId));
    }

    [Authorize]
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> RevokeSession(string sessionId)
    {
        if (!await IsCsrfRequestValidAsync())
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid CSRF token", detail: "The request could not be verified.");
        }

        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthenticated", detail: "Please sign in again.");
        }

        if (!Guid.TryParseExact(sessionId, "N", out _))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid session", detail: "The session identifier is invalid.");
        }

        var result = await _authService.RevokeSessionAsync(_currentUser.UserId, sessionId, _currentUser.SessionId);

        if (!result.Revoked)
        {
            return NotFound();
        }

        if (result.RevokedCurrentSession)
        {
            DeleteRefreshTokenCookie();
        }

        _logger.LogInformation("User session revoked for user {UserId}; current session: {RevokedCurrentSession}", _currentUser.UserId, result.RevokedCurrentSession);
        return Ok(new { result.RevokedCurrentSession });
    }

    private void SetRefreshTokenCookie(AuthSession session)
    {
        var options = CreateRefreshTokenCookieOptions();

        if (session.KeepMeSignedIn)
        {
            options.Expires = new DateTimeOffset(session.RefreshTokenExpiresAt);
        }

        Response.Cookies.Append(_refreshTokenSettings.CookieName, session.RefreshToken, options);
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete(_refreshTokenSettings.CookieName, CreateRefreshTokenCookieOptions());
    }

    private CookieOptions CreateRefreshTokenCookieOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = _useSecureCookies,
            SameSite = _useSecureCookies ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/api/auth"
        };
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

    private ClientSessionInfo CreateClientSessionInfo()
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        return new ClientSessionInfo
        {
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? "Unknown device" : userAgent[..Math.Min(userAgent.Length, 512)],
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
        };
    }
}
