using UserManagement.Core.Models;

namespace UserManagement.DataAccess.Interfaces;

public interface IUserRepository
{
    Task<List<User>> GetUsersAsync();
    Task<User?> GetByIdAsync(string id);

    Task<User?> GetByEmailAsync(string email);
    Task CreateAsync(User user);
    Task<User?> UpdateProfileAsync(string id, string name, string email);
    Task<User?> UpdateImageUrlAsync(string id, string imageUrl);
    Task<User?> RemoveImageUrlAsync(string id);
}