using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;

namespace UserManagement.Web.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        ICurrentUserService currentUser,
        ILogger<AuthController> logger
        )
    {
        _authService = authService;
        _currentUser = currentUser;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("signup")]
    public async Task<IActionResult> Signup(
        SignupRequest request)
    {
        var result = await _authService.SignupAsync(request);

        if (result is null)
        {
            _logger.LogWarning("Signup rejected because email already exists");
            return Conflict(new
            {
                message = "Email is already signed up."
            });
        }
        _logger.LogInformation("User signup completed");
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("signin")]
    public async Task<IActionResult> Signin(
        SigninRequest request)
    {
        var result = await _authService.SigninAsync(request);

        if (result is null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        return Ok(result);
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
}