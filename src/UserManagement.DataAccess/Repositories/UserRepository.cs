using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;
using UserManagement.DataAccess.Database;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.DataAccess.Repositories;

public class UserRepository : MongoDBClient, IUserRepository
{
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IAuditActorProvider _auditActorProvider;

    public UserRepository(
        IOptions<MongoDbSettings> options,
        IAuditActorProvider auditActorProvider)
        : base(options)
    {
        _auditActorProvider = auditActorProvider;
        _usersCollection =
            MongoDb.GetCollection<User>("users");
    }

    public async Task<List<User>> GetUsersAsync(int skip, int take)
    {
        return await _usersCollection
            .Find(Builders<User>.Filter.Empty)
            .SortByDescending(user => user.CreatedOn)
            .ThenBy(user => user.Id)
            .Skip(skip)
            .Limit(take)
            .ToListAsync();
    }

    public async Task<long> CountUsersAsync()
    {
        return await _usersCollection.CountDocumentsAsync(Builders<User>.Filter.Empty);
    }

    public async Task<long> CountActiveAdminsAsync()
    {
        return await _usersCollection.CountDocumentsAsync(user => user.Role == UserRoles.Admin && user.Status == AccountStatus.Active);
    }

    public async Task PingAsync(CancellationToken cancellationToken)
    {
        await MongoDb.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _usersCollection
            .Find(user => user.Email == email)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(User user)
    {
        AuditStamp.Created(user, _auditActorProvider.ActorId);
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
            .Set(user => user.ModifiedOn, DateTime.UtcNow)
            .Set(user => user.ModifiedBy, _auditActorProvider.ActorId);

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
            .Set(user => user.ModifiedOn, DateTime.UtcNow)
            .Set(user => user.ModifiedBy, _auditActorProvider.ActorId);

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
            .Set(user => user.ModifiedOn, DateTime.UtcNow)
            .Set(user => user.ModifiedBy, _auditActorProvider.ActorId);

        return await _usersCollection.FindOneAndUpdateAsync(user => user.Id == id, update, new FindOneAndUpdateOptions<User>
            {
                ReturnDocument = ReturnDocument.After
            }
        );
    }

    public async Task<User?> IncrementFailedSigninAttemptsAsync(string id)
    {
        var update = Builders<User>.Update.Inc(user => user.FailedSigninAttempts, 1).Set(user => user.ModifiedOn, DateTime.UtcNow).Set(user => user.ModifiedBy, _auditActorProvider.ActorId);
        return await _usersCollection.FindOneAndUpdateAsync(user => user.Id == id, update, new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After });
    }

    public async Task ResetSigninFailuresAsync(string id)
    {
        var update = Builders<User>.Update.Set(user => user.FailedSigninAttempts, 0).Set(user => user.LockoutEnd, null).Set(user => user.ModifiedOn, DateTime.UtcNow).Set(user => user.ModifiedBy, _auditActorProvider.ActorId);
        await _usersCollection.UpdateOneAsync(user => user.Id == id, update);
    }

    public async Task SetLastSignInOnAsync(string id, DateTime lastSignInOn)
    {
        await _usersCollection.UpdateOneAsync(user => user.Id == id, Builders<User>.Update.Set(user => user.LastSignInOn, lastSignInOn));
    }

    public async Task SetLockoutEndAsync(string id, DateTime lockoutEnd)
    {
        var update = Builders<User>.Update.Set(user => user.LockoutEnd, lockoutEnd).Set(user => user.ModifiedOn, DateTime.UtcNow).Set(user => user.ModifiedBy, _auditActorProvider.ActorId);
        await _usersCollection.UpdateOneAsync(user => user.Id == id, update);
    }

    public async Task SetEmailVerifiedAsync(string id)
    {
        var update = Builders<User>.Update.Set(user => user.IsEmailVerified, true).Set(user => user.ModifiedOn, DateTime.UtcNow).Set(user => user.ModifiedBy, _auditActorProvider.ActorId);
        await _usersCollection.UpdateOneAsync(user => user.Id == id, update);
    }

    public async Task<bool> UpdatePasswordHashAndIncrementTokenVersionAsync(string id, string passwordHash)
    {
        var update = Builders<User>.Update
            .Set(user => user.PasswordHash, passwordHash)
            .Inc(user => user.TokenVersion, 1)
            .Set(user => user.FailedSigninAttempts, 0)
            .Set(user => user.LockoutEnd, null)
            .Set(user => user.ModifiedOn, DateTime.UtcNow)
            .Set(user => user.ModifiedBy, _auditActorProvider.ActorId);
        var result = await _usersCollection.UpdateOneAsync(user => user.Id == id, update);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> TryAcquireAccountEmailCooldownAsync(string id, AccountTokenPurpose purpose, DateTime now, DateTime nextAllowedOn, CancellationToken cancellationToken = default)
    {
        var userFilter = Builders<User>.Filter.Eq(user => user.Id, id);
        FilterDefinition<User> cooldownFilter;
        UpdateDefinition<User> update;

        if (purpose == AccountTokenPurpose.EmailVerification)
        {
            cooldownFilter = Builders<User>.Filter.Or(Builders<User>.Filter.Eq(user => user.NextEmailVerificationAllowedOn, null), Builders<User>.Filter.Lte(user => user.NextEmailVerificationAllowedOn, now));
            update = Builders<User>.Update.Set(user => user.NextEmailVerificationAllowedOn, nextAllowedOn).Set(user => user.ModifiedOn, now).Set(user => user.ModifiedBy, _auditActorProvider.ActorId);
        }
        else
        {
            cooldownFilter = Builders<User>.Filter.Or(Builders<User>.Filter.Eq(user => user.NextPasswordResetAllowedOn, null), Builders<User>.Filter.Lte(user => user.NextPasswordResetAllowedOn, now));
            update = Builders<User>.Update.Set(user => user.NextPasswordResetAllowedOn, nextAllowedOn).Set(user => user.ModifiedOn, now).Set(user => user.ModifiedBy, _auditActorProvider.ActorId);
        }

        var result = await _usersCollection.UpdateOneAsync(userFilter & cooldownFilter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var result = await _usersCollection.DeleteOneAsync(user => user.Id == id, cancellationToken);
        return result.DeletedCount == 1;
    }

    public async Task<User?> ChangeRoleAsync(string id, string role, CancellationToken cancellationToken = default)
    {
        var update = Builders<User>.Update.Set(user => user.Role, role).Inc(user => user.TokenVersion, 1).Set(user => user.ModifiedOn, DateTime.UtcNow).Set(user => user.ModifiedBy, _auditActorProvider.ActorId);
        return await _usersCollection.FindOneAndUpdateAsync(user => user.Id == id, update, new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After }, cancellationToken);
    }

    public async Task<User?> ChangeStatusAsync(string id, AccountStatus status, string changedByUserId, CancellationToken cancellationToken = default)
    {
        var disabled = status == AccountStatus.Disabled;
        var update = Builders<User>.Update
            .Set(user => user.Status, status)
            .Set(user => user.DisabledOn, disabled ? DateTime.UtcNow : null)
            .Set(user => user.DisabledByUserId, disabled ? changedByUserId : null)
            .Set(user => user.FailedSigninAttempts, 0)
            .Set(user => user.LockoutEnd, null)
            .Inc(user => user.TokenVersion, 1)
            .Set(user => user.ModifiedOn, DateTime.UtcNow)
            .Set(user => user.ModifiedBy, _auditActorProvider.ActorId);
        return await _usersCollection.FindOneAndUpdateAsync(user => user.Id == id, update, new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After }, cancellationToken);
    }

    public async Task<User?> UnlockAsync(string id, CancellationToken cancellationToken = default)
    {
        var update = Builders<User>.Update.Set(user => user.FailedSigninAttempts, 0).Set(user => user.LockoutEnd, null).Set(user => user.ModifiedOn, DateTime.UtcNow).Set(user => user.ModifiedBy, _auditActorProvider.ActorId);
        return await _usersCollection.FindOneAndUpdateAsync(user => user.Id == id, update, new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After }, cancellationToken);
    }
}
