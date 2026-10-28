using Microsoft.AspNetCore.Mvc;
using UserManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.Web.Validation;

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
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> GetUsers([FromQuery] PaginationRequest request)
    {
        _logger.LogInformation("Getting all users");

        var users = await _userService.GetUsersAsync(request.Page, request.PageSize);

        _logger.LogInformation("Retrieved page {Page} containing {UserCount} users", users.Page, users.Items.Count);

        return Ok(users);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{UserRoles.User},{UserRoles.Admin}")]
    public async Task<IActionResult> GetUser([MongoObjectId] string id)
    {
        var currentUserId = _currentUser.UserId;
        var isAdmin = _currentUser.IsAdmin;

        if (currentUserId != id && !isAdmin)
        {
            _logger.LogWarning("Access denied for user {CurrentUserId} targeting user {TargetUserId}", currentUserId, id);
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not allowed to access this user.");
        }

        var user = await _userService.GetUserByIdAsync(id);

        return user is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found", detail: "The requested user does not exist.")
            : Ok(user);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{UserRoles.User},{UserRoles.Admin}")]
    public async Task<IActionResult> UpdateProfile([MongoObjectId] string id, [FromBody] UpdateUserProfileRequest request)
    {
        var currentUserId = _currentUser.UserId;
        var isAdmin = _currentUser.IsAdmin;

        if (currentUserId != id && !isAdmin)
        {
            _logger.LogWarning("Access denied for user {CurrentUserId} targeting user {TargetUserId}", currentUserId, id);
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not allowed to update this user.");
        }

        var updatedUser = await _userService.UpdateProfileAsync(id, request);

        return updatedUser is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found", detail: "The requested user does not exist.")
            : Ok(updatedUser);
    }

    [HttpPut("{id}/profile-picture")]
    [Authorize(Roles = $"{UserRoles.User},{UserRoles.Admin}")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UpdateImageUrl([MongoObjectId] string id, [FromForm] IFormFile? image, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId;
        var isAdmin = _currentUser.IsAdmin;

        if (currentUserId != id && !isAdmin)
        {
            _logger.LogWarning("Access denied for user {CurrentUserId} targeting user {TargetUserId}", currentUserId, id);
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not allowed to update this user.");
        }

        if (image is null || image.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Image required", detail: "Select an image to upload.");
        }

        const long maximumSize = 2 * 1024 * 1024;

        if (image.Length > maximumSize)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Image too large", detail: "Image must be 2 MB or smaller.");
        }

        var allowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png"
        };

        if (!allowedTypes.TryGetValue(image.ContentType, out var extension))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unsupported image type", detail: "Only JPEG and PNG are allowed.");
        }

        await using var stream = image.OpenReadStream();

        if (!await ProfileImageValidator.HasExpectedSignatureAsync(stream, image.ContentType, cancellationToken))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid image file", detail: "The image content does not match its declared type.");
        }

        _logger.LogInformation("Uploading profile image for user {UserId}, type {ContentType}, size {Size}", id, image.ContentType, image.Length);
        var user = await _userService.UpdateImageUrlAsync(id, stream, extension, image.ContentType, cancellationToken);
        _logger.LogInformation("Profile image updated for user {UserId}", id);
        return user is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found", detail: "The requested user does not exist.")
            : Ok(user);
    }

    [HttpDelete("{id}/profile-picture")]
    [Authorize(Roles = $"{UserRoles.User},{UserRoles.Admin}")]
    public async Task<IActionResult> RemoveImageUrlAsync([MongoObjectId] string id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId;
        var isAdmin = _currentUser.IsAdmin;

        if (currentUserId != id && !isAdmin)
        {
            _logger.LogWarning("Access denied for user {CurrentUserId} targeting user {TargetUserId}", currentUserId, id);
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "You are not allowed to update this user.");
        }

        var user = await _userService.RemoveImageUrlAsync(id, cancellationToken);
        _logger.LogInformation("Profile image removed for user {UserId}", id);
        return user is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found", detail: "The requested user does not exist.")
            : Ok(user);
    }
}
