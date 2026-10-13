using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using UserManagement.Core.Models;
using UserManagement.DataAccess.Database;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.DataAccess.Repositories;

public class UserRepository : MongoDBClient, IUserRepository
{
    private readonly IMongoCollection<User> _usersCollection;

    public UserRepository(IConfiguration configuration)
        : base(configuration)
    {
        _usersCollection =
            MongoDb.GetCollection<User>("users");
    }

    public async Task<List<User>> GetUsersAsync()
    {
        return await _usersCollection
            .Find(Builders<User>.Filter.Empty)
            .ToListAsync();
    }
}