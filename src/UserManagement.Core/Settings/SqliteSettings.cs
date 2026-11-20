namespace UserManagement.Core.Settings;

public sealed class SqliteSettings
{
    public string DatabasePath { get; set; } = "/data/database/user-management-api.db";
}
