using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;
using UserManagement.DataAccess.Database;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.DataAccess.Repositories;

public class UserRepository : MongoDBClient, IUserRepository
{
    private readonly IMongoCollection<User> _usersCollection;

    public UserRepository(
        IOptions<MongoDbSettings> options)
        : base(options)
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

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _usersCollection
            .Find(user => user.Email == email)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(User user)
    {
        await _usersCollection.InsertOneAsync(user);
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        return await _usersCollection
            .Find(user => user.Id == id)
            .FirstOrDefaultAsync();
    }
}