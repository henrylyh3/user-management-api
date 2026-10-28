using UserManagement.Core.DTOs;

namespace UserManagement.Application.Interfaces;

public interface IUserService
{
    Task<PagedResult<AdminUserDto>> GetUsersAsync(int page, int pageSize);
    Task<UserDto?> GetUserByIdAsync(string id);

    Task<UserDto?> UpdateProfileAsync(string id, UpdateUserProfileRequest request);

    Task<UserDto?> UpdateImageUrlAsync(string id, Stream imageStream, string extension, string contentType, CancellationToken cancellationToken = default);
    Task<UserDto?> RemoveImageUrlAsync(string id, CancellationToken cancellationToken = default);
}
