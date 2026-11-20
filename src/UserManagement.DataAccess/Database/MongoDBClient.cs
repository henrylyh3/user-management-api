using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UserManagement.Core.Settings;

namespace UserManagement.DataAccess.Database;

public class MongoDBClient
{
    protected IMongoDatabase MongoDb { get; }

    public MongoDBClient(
        IOptions<MongoDbSettings> options)
    {
        var settings = options.Value;

        var clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
        if (settings.Tls.HasValue) clientSettings.UseTls = settings.Tls.Value;
        if (settings.DirectConnection.HasValue) clientSettings.DirectConnection = settings.DirectConnection.Value;
        var client = new MongoClient(clientSettings);

        MongoDb = client.GetDatabase(
            settings.DatabaseName
        );
    }
}