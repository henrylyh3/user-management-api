using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;
using UserManagement.DataAccess.Database;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.DataAccess.Repositories;

public sealed class RefreshTokenRepository : MongoDBClient, IRefreshTokenRepository
{
    private readonly IMongoCollection<RefreshToken> _refreshTokens;
    private readonly IAuditActorProvider _auditActorProvider;

    public RefreshTokenRepository(IOptions<MongoDbSettings> options, IAuditActorProvider auditActorProvider) : base(options)
    {
        _auditActorProvider = auditActorProvider;
        _refreshTokens = MongoDb.GetCollection<RefreshToken>("refreshTokens");
    }

    public async Task CreateAsync(RefreshToken refreshToken)
    {
        AuditStamp.Created(refreshToken, _auditActorProvider.ActorId);
        await _refreshTokens.InsertOneAsync(refreshToken);
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
    {
        return await _refreshTokens.Find(token => token.TokenHash == tokenHash).FirstOrDefaultAsync();
    }

    public async Task<bool> TryRevokeAsync(string id, string? replacedByTokenHash = null)
    {
        var filter = Builders<RefreshToken>.Filter.Where(token => token.Id == id && token.RevokedOn == null && token.ExpiresAt > DateTime.UtcNow);
        var update = Builders<RefreshToken>.Update.Set(token => token.RevokedOn, DateTime.UtcNow).Set(token => token.ReplacedByTokenHash, replacedByTokenHash).Set(token => token.ModifiedOn, DateTime.UtcNow).Set(token => token.ModifiedBy, _auditActorProvider.ActorId);
        var result = await _refreshTokens.UpdateOneAsync(filter, update);

        return result.ModifiedCount == 1;
    }

    public async Task RevokeAllByUserIdAsync(string userId)
    {
        var filter = Builders<RefreshToken>.Filter.Where(token => token.UserId == userId && token.RevokedOn == null);
        var update = Builders<RefreshToken>.Update.Set(token => token.RevokedOn, DateTime.UtcNow).Set(token => token.ModifiedOn, DateTime.UtcNow).Set(token => token.ModifiedBy, _auditActorProvider.ActorId);

        await _refreshTokens.UpdateManyAsync(filter, update);
    }

    public async Task<List<RefreshToken>> GetActiveByUserIdAsync(string userId)
    {
        return await _refreshTokens.Find(token => token.UserId == userId && token.SessionId != string.Empty && token.RevokedOn == null && token.ExpiresAt > DateTime.UtcNow).SortByDescending(token => token.LastSeenOn).ToListAsync();
    }

    public async Task<bool> IsSessionActiveAsync(string userId, string sessionId)
    {
        return await _refreshTokens.Find(token => token.UserId == userId && token.SessionId == sessionId && token.RevokedOn == null && token.ExpiresAt > DateTime.UtcNow).AnyAsync();
    }

    public async Task<bool> RevokeSessionAsync(string userId, string sessionId)
    {
        var filter = Builders<RefreshToken>.Filter.Where(token => token.UserId == userId && token.SessionId == sessionId && token.RevokedOn == null && token.ExpiresAt > DateTime.UtcNow);
        var update = Builders<RefreshToken>.Update.Set(token => token.RevokedOn, DateTime.UtcNow).Set(token => token.ModifiedOn, DateTime.UtcNow).Set(token => token.ModifiedBy, _auditActorProvider.ActorId);
        var result = await _refreshTokens.UpdateManyAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _refreshTokens.DeleteManyAsync(token => token.UserId == userId, cancellationToken);
    }
}
