using UserManagement.Core.Models;

namespace UserManagement.DataAccess.Interfaces;

public interface IUserRepository
{
    Task<List<User>> GetUsersAsync();
    Task<User?> GetByEmailAsync(string email);
    Task CreateAsync(User user);
}