using UserManagement.Core.Models;

namespace UserManagement.DataAccess.Interfaces;

public interface IUserRepository
{
    Task<List<User>> GetUsersAsync();
}