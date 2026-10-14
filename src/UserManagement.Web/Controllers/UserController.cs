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
    private readonly ICurrentUserService _currentUser;

    public UserController(
        IUserService userService,
        ILogger<UserController> logger,
        ICurrentUserService currentUser)
    {
        _userService = userService;
        _logger = logger;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
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

    [HttpGet("{id}")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetUser(string id)
    {
        var currentUserId = _currentUser.UserId;
        var isAdmin = _currentUser.IsAdmin;

        if (currentUserId != id && !isAdmin)
        {
            return Forbid();
        }

        var user = await _userService.GetUserByIdAsync(id);

        return user is null ? NotFound() : Ok(user);
    }
}