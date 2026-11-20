using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using UserManagement.Core.Settings;

namespace UserManagement.DataAccess.Database;

public sealed class SqliteDatabase
{
    private readonly string _connectionString;

    public SqliteDatabase(IOptions<SqliteSettings> options)
    {
        var path = options.Value.DatabasePath;
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Sqlite:DatabasePath is required.");
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, DefaultTimeout = 5 }.ToString();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS users (
                Id TEXT PRIMARY KEY NOT NULL,
                FirstName TEXT NOT NULL,
                LastName TEXT NOT NULL,
                Email TEXT NOT NULL COLLATE NOCASE,
                CreatedOn TEXT NOT NULL,
                UpdatedOn TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                Role TEXT NOT NULL CHECK (Role IN ('User', 'Admin')),
                ImageUrl TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS UX_users_Email ON users (Email);
            """;
        command.ExecuteNonQuery();
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
