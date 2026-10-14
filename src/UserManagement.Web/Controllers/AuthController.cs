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

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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
}