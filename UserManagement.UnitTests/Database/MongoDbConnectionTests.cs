using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UserManagement.Core.Settings;
using UserManagement.DataAccess.Database;

namespace UserManagement.UnitTests.Database;

public sealed class MongoDbConnectionTests
{
    private sealed class InspectableClient(MongoDbSettings settings) : MongoDBClient(Options.Create(settings))
    {
        public MongoClientSettings ConnectionSettings => MongoDb.Client.Settings;
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ExplicitSettings_OverrideConnectionString(bool tls, bool direct)
    {
        var client = new InspectableClient(new MongoDbSettings
        {
            ConnectionString = $"mongodb://127.0.0.1:27017/?tls={!tls}&directConnection={!direct}",
            DatabaseName = "configuration-tests",
            Tls = tls,
            DirectConnection = direct
        });

        Assert.Equal(tls, client.ConnectionSettings.UseTls);
        Assert.Equal(direct, client.ConnectionSettings.DirectConnection);
    }

    [Fact]
    public void UnspecifiedSettings_PreserveConnectionStringOptions()
    {
        var client = new InspectableClient(new MongoDbSettings
        {
            ConnectionString = "mongodb://127.0.0.1:27017/?tls=true&directConnection=false",
            DatabaseName = "configuration-tests"
        });

        Assert.True(client.ConnectionSettings.UseTls);
        Assert.False(client.ConnectionSettings.DirectConnection);
    }
}
