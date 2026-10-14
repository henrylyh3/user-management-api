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

        var client = new MongoClient(
            settings.ConnectionString
        );

        MongoDb = client.GetDatabase(
            settings.DatabaseName
        );
    }
}