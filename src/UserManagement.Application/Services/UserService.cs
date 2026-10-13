using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        var users = await _userRepository.GetUsersAsync();

        return users
            .Select(user => new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email
            })
            .ToList();
    }
}