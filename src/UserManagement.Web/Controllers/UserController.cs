using Microsoft.AspNetCore.Mvc;
using UserManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using UserManagement.Core.DTOs;

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

        _logger.LogInformation( "Retrieved {UserCount} users", users.Count);

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

    [HttpPut("{id}/profile")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> UpdateProfile( string id, [FromBody] UpdateUserProfileRequest request)
    {
        var currentUserId = _currentUser.UserId;
        var isAdmin = _currentUser.IsAdmin;

        if (currentUserId != id && !isAdmin)
        {
            return Forbid();
        }

        var updatedUser = await _userService.UpdateProfileAsync(id, request);

        return updatedUser is null ? NotFound() : Ok(updatedUser);
    }

    [HttpPut("{id}/profile-picture")]
    [Authorize(Roles = "User,Admin")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UpdateImageUrl(string id, [FromForm] IFormFile? image, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId;
        var isAdmin = _currentUser.IsAdmin;

        if (currentUserId != id && !isAdmin)
        {
            return Forbid();
        }

        if (image is null || image.Length == 0)
        {
            return BadRequest("An image is required.");
        }

        const long maximumSize = 2 * 1024 * 1024;

        if (image.Length > maximumSize)
        {
            return BadRequest("Image must be 2 MB or smaller.");
        }

        var allowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["image/jpeg"] = ".jpg",
                ["image/png"] = ".png"
            };

        if (!allowedTypes.TryGetValue(image.ContentType, out var extension))
        {
            return BadRequest("Only JPEG, PNG are allowed.");
        }

        await using var stream = image.OpenReadStream();

        var user = await _userService.UpdateImageUrlAsync(id, stream, extension, image.ContentType, cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    [HttpDelete("{id}/profile-picture")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> RemoveImageUrlAsync(string id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId;
        var isAdmin = _currentUser.IsAdmin;

        if (currentUserId != id && !isAdmin)
        {
            return Forbid();
        }

        var user = await _userService.RemoveImageUrlAsync(id, cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }
}