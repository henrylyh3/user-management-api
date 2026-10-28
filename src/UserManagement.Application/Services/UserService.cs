using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.DataAccess.Interfaces;
using UserManagement.Core.Models;

namespace UserManagement.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IAuditEventWriter _auditEventWriter;

    public UserService(
        IUserRepository userRepository,
        IFileStorageService fileStorage,
        IAuditEventWriter auditEventWriter
    )
    {
        _userRepository = userRepository;
        _fileStorage = fileStorage;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<PagedResult<AdminUserDto>> GetUsersAsync(int page, int pageSize)
    {
        var users = await _userRepository.GetUsersAsync((page - 1) * pageSize, pageSize);
        var totalCount = await _userRepository.CountUsersAsync();

        var items = users
            .Select(user => new AdminUserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                ImageUrl = string.IsNullOrWhiteSpace(user.ImageUrl) ? null : _fileStorage.GetUrl(user.ImageUrl),
                Role = user.Role,
                Status = user.Status.ToString(),
                LockoutEnd = user.LockoutEnd,
                IsLockedOut = user.LockoutEnd > DateTime.UtcNow,
                LastSignInOn = user.LastSignInOn,
                CreatedOn = user.CreatedOn,
                ModifiedOn = user.ModifiedOn,
                CreatedBy = user.CreatedBy,
                ModifiedBy = user.ModifiedBy
            })
            .ToList();

        return new PagedResult<AdminUserDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }
    public async Task<UserDto?> GetUserByIdAsync(string id)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user is null)
        {
            return null;
        }

        return new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            ImageUrl = string.IsNullOrWhiteSpace(user.ImageUrl) ? null : _fileStorage.GetUrl(user.ImageUrl)
        };
    }

    public async Task<UserDto?> UpdateProfileAsync(string id, UpdateUserProfileRequest request)
    {
        var user = await _userRepository.UpdateProfileAsync(id, request.FirstName.Trim(), request.LastName.Trim());

        if (user is null)
        {
            return null;
        }

        await WriteAuditAsync("ProfileUpdated", user.Id);
        return new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            ImageUrl = string.IsNullOrWhiteSpace(user.ImageUrl) ? null : _fileStorage.GetUrl(user.ImageUrl)
        };
    }

    public async Task<UserDto?> UpdateImageUrlAsync(string id, Stream imageStream, string extension, string contentType, CancellationToken cancellationToken = default)
    {
        var existingUser = await _userRepository.GetByIdAsync(id);

        if (existingUser is null)
        {
            return null;
        }

        var newImageUrl = $"profile-images/{id}/{Guid.NewGuid():N}{extension}";
        // Upload the replacement first so the existing image remains available if upload fails
        await _fileStorage.UploadAsync(imageStream, newImageUrl, contentType, cancellationToken);

        User? updatedUser;

        try
        {
            updatedUser = await _userRepository.UpdateImageUrlAsync(id, newImageUrl);
        }
        catch
        {
            await _fileStorage.DeleteAsync(newImageUrl, cancellationToken);
            throw;
        }

        if (updatedUser is null)
        {
            await _fileStorage.DeleteAsync(newImageUrl, cancellationToken);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(existingUser.ImageUrl))
        {
            await _fileStorage.DeleteAsync(existingUser.ImageUrl, cancellationToken);
        }

        await WriteAuditAsync("ProfileImageUpdated", updatedUser.Id, cancellationToken);
        return new UserDto
        {
            Id = updatedUser.Id,
            FirstName = updatedUser.FirstName,
            LastName = updatedUser.LastName,
            Email = updatedUser.Email,
            ImageUrl = _fileStorage.GetUrl(updatedUser.ImageUrl!)
        };
    }

    public async Task<UserDto?> RemoveImageUrlAsync(string id, CancellationToken cancellationToken = default)
    {
        var existingUser = await _userRepository.GetByIdAsync(id);

        if (existingUser is null)
        {
            return null;
        }

        //remove db
        var updatedUser = await _userRepository.RemoveImageUrlAsync(id);

        if (updatedUser is null)
        {
            return null;
        }

        //delete image
        if (!string.IsNullOrWhiteSpace(existingUser.ImageUrl))
        {
            await _fileStorage.DeleteAsync(existingUser.ImageUrl, cancellationToken);
        }

        await WriteAuditAsync("ProfileImageRemoved", updatedUser.Id, cancellationToken);
        return new UserDto
        {
            Id = updatedUser.Id,
            FirstName = updatedUser.FirstName,
            LastName = updatedUser.LastName,
            Email = updatedUser.Email,
            ImageUrl = null
        };
    }

    private Task WriteAuditAsync(string action, string userId, CancellationToken cancellationToken = default)
    {
        return _auditEventWriter.WriteAsync(new AuditEventRequest { Module = "UserProfile", Action = action, Outcome = AuditOutcome.Success, ResourceType = "User", ResourceId = userId }, cancellationToken);
    }
}
