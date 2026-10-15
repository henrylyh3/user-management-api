using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.DataAccess.Interfaces;
using UserManagement.Core.Models;

namespace UserManagement.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IFileStorageService _fileStorage;

    public UserService(
        IUserRepository userRepository,
        IFileStorageService fileStorage
    )
    {
        _userRepository = userRepository;
        _fileStorage = fileStorage;
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        var users = await _userRepository.GetUsersAsync();

        return users
            .Select(user => new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                ImageUrl = string.IsNullOrWhiteSpace(user.ImageUrl) ? null : _fileStorage.GetUrl(user.ImageUrl)
            })
            .ToList();
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
            await _fileStorage.DeleteAsync( newImageUrl, cancellationToken);
            return null;
        }

        if (!string.IsNullOrWhiteSpace( existingUser.ImageUrl))
        {
            await _fileStorage.DeleteAsync( existingUser.ImageUrl,cancellationToken);
        }

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
            await _fileStorage.DeleteAsync( existingUser.ImageUrl, cancellationToken);
        }


        return new UserDto
        {
            Id = updatedUser.Id,
            FirstName = updatedUser.FirstName,
            LastName = updatedUser.LastName,
            Email = updatedUser.Email,
            ImageUrl = null
        };
    }
}