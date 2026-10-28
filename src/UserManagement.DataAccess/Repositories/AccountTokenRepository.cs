using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;
using UserManagement.DataAccess.Database;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.DataAccess.Repositories;

public sealed class AccountTokenRepository : MongoDBClient, IAccountTokenRepository
{
    private readonly IMongoCollection<AccountToken> _accountTokens;
    private readonly IAuditActorProvider _auditActorProvider;

    public AccountTokenRepository(IOptions<MongoDbSettings> options, IAuditActorProvider auditActorProvider) : base(options)
    {
        _auditActorProvider = auditActorProvider;
        _accountTokens = MongoDb.GetCollection<AccountToken>("accountTokens");
    }

    public async Task CreateAsync(AccountToken accountToken, CancellationToken cancellationToken = default)
    {
        AuditStamp.Created(accountToken, _auditActorProvider.ActorId);
        await _accountTokens.InsertOneAsync(accountToken, cancellationToken: cancellationToken);
    }

    public async Task<AccountToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _accountTokens.Find(token => token.TokenHash == tokenHash).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryConsumeAsync(string id, DateTime consumedOn, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AccountToken>.Filter.Where(token => token.Id == id && token.ConsumedOn == null && token.ExpiresAt > consumedOn);
        var update = Builders<AccountToken>.Update.Set(token => token.ConsumedOn, consumedOn).Set(token => token.ModifiedOn, consumedOn).Set(token => token.ModifiedBy, _auditActorProvider.ActorId);
        var result = await _accountTokens.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task RevokeActiveByUserIdAndPurposeAsync(string userId, AccountTokenPurpose purpose, DateTime revokedOn, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AccountToken>.Filter.Where(token => token.UserId == userId && token.Purpose == purpose && token.ConsumedOn == null);
        var update = Builders<AccountToken>.Update.Set(token => token.ConsumedOn, revokedOn).Set(token => token.ModifiedOn, revokedOn).Set(token => token.ModifiedBy, _auditActorProvider.ActorId);
        await _accountTokens.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _accountTokens.DeleteManyAsync(token => token.UserId == userId, cancellationToken);
    }
}
