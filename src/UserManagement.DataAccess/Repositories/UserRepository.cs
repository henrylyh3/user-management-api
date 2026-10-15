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

    public async Task<User?> UpdateProfileAsync(string id, string firstName, string lastName)
    {
        var update = Builders<User>.Update
            .Set(user => user.FirstName, firstName)
            .Set(user => user.LastName, lastName)
            .Set(user => user.UpdatedOn, DateTime.UtcNow);

        return await _usersCollection.FindOneAndUpdateAsync(
            user => user.Id == id,
            update,
            new FindOneAndUpdateOptions<User>
            {
                ReturnDocument = ReturnDocument.After
            });
    }

    public async Task<User?> UpdateImageUrlAsync( string id, string imageUrl)
    {
        var update = Builders<User>.Update
            .Set(user => user.ImageUrl, imageUrl)
            .Set(user => user.UpdatedOn, DateTime.UtcNow);

        return await _usersCollection.FindOneAndUpdateAsync(user => user.Id == id, update, new FindOneAndUpdateOptions<User>
            {
                ReturnDocument = ReturnDocument.After
            }
        );
    }

    public async Task<User?> RemoveImageUrlAsync( string id )
    {
        var update = Builders<User>.Update
            .Set(user => user.ImageUrl, null)
            .Set(user => user.UpdatedOn, DateTime.UtcNow);

        return await _usersCollection.FindOneAndUpdateAsync(user => user.Id == id, update, new FindOneAndUpdateOptions<User>
            {
                ReturnDocument = ReturnDocument.After
            }
        );
    }
}