using Microsoft.AspNetCore.Mvc;
using UserManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace UserManagement.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IUserService userService,
        ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetUsers()
    {
        _logger.LogInformation("Getting all users");

        var users = await _userService.GetUsersAsync();

        _logger.LogInformation(
            "Retrieved {UserCount} users",
            users.Count
        );

        return Ok(users);
    }
}