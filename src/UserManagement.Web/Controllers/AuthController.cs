using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;

namespace UserManagement.Web.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(
        IAuthService authService,
        ICurrentUserService currentUser
        )
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    [AllowAnonymous]
    [HttpPost("signup")]
    public async Task<IActionResult> Signup(
        SignupRequest request)
    {
        var result = await _authService.SignupAsync(request);

        if (result is null)
        {
            return Conflict(new
            {
                message = "Email is already signed up."
            });
        }

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