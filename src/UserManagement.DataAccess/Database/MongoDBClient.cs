using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace UserManagement.DataAccess.Database;

public class MongoDBClient
{
    protected IMongoDatabase MongoDb { get; }

    public MongoDBClient(IConfiguration configuration)
    {
        var connectionString =
            configuration["MongoDB:ConnectionString"];

        var databaseName =
            configuration["MongoDB:DatabaseName"];

        var client = new MongoClient(connectionString);

        MongoDb = client.GetDatabase(databaseName);
    }
}