using UserManagement.Core.DTOs;

namespace UserManagement.Application.Interfaces;

public interface IUserService
{
    Task<List<UserDto>> GetUsersAsync();
    Task<UserDto?> GetUserByIdAsync(string id);
}