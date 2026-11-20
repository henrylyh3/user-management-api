namespace UserManagement.Core.Settings;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;

    public bool? Tls { get; set; }

    public bool? DirectConnection { get; set; }
}