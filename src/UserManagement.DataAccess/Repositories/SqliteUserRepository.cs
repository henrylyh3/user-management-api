using System.Globalization;
using Microsoft.Data.Sqlite;
using MongoDB.Bson;
using UserManagement.Core.Models;
using UserManagement.DataAccess.Database;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.DataAccess.Repositories;

public sealed class SqliteUserRepository(SqliteDatabase database) : IUserRepository
{
    private const string Columns = "Id, FirstName, LastName, Email, CreatedOn, UpdatedOn, PasswordHash, Role, ImageUrl";

    public async Task<List<User>> GetUsersAsync()
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM users ORDER BY CreatedOn, Id";
        using var reader = await command.ExecuteReaderAsync();
        var users = new List<User>();
        while (await reader.ReadAsync()) users.Add(ReadUser(reader));
        return users;
    }

    public Task<User?> GetByIdAsync(string id) => FindAsync("Id", id);
    public Task<User?> GetByEmailAsync(string email) => FindAsync("Email", email);

    private async Task<User?> FindAsync(string column, string value)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        // The column comes exclusively from the two fixed methods above.
        command.CommandText = $"SELECT {Columns} FROM users WHERE {column} = $value";
        command.Parameters.AddWithValue("$value", value);
        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadUser(reader) : null;
    }

    public async Task CreateAsync(User user)
    {
        if (string.IsNullOrEmpty(user.Id)) user.Id = ObjectId.GenerateNewId().ToString();
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO users ({Columns}) VALUES ($id, $firstName, $lastName, $email, $createdOn, $updatedOn, $passwordHash, $role, $imageUrl)";
        command.Parameters.AddWithValue("$id", user.Id);
        command.Parameters.AddWithValue("$firstName", user.FirstName);
        command.Parameters.AddWithValue("$lastName", user.LastName);
        command.Parameters.AddWithValue("$email", user.Email);
        command.Parameters.AddWithValue("$createdOn", user.CreatedOn.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updatedOn", user.UpdatedOn.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$role", user.Role);
        command.Parameters.AddWithValue("$imageUrl", (object?)user.ImageUrl ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public Task<User?> UpdateProfileAsync(string id, string firstName, string lastName) => UpdateAsync(id, "FirstName = $firstName, LastName = $lastName", new SqliteParameter("$firstName", firstName), new SqliteParameter("$lastName", lastName));
    public Task<User?> UpdateImageUrlAsync(string id, string imageUrl) => UpdateAsync(id, "ImageUrl = $imageUrl", new SqliteParameter("$imageUrl", imageUrl));
    public Task<User?> RemoveImageUrlAsync(string id) => UpdateAsync(id, "ImageUrl = NULL");

    private async Task<User?> UpdateAsync(string id, string assignments, params SqliteParameter[] parameters)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        // Assignment SQL is fixed by this class; all user input uses parameters.
        command.CommandText = $"UPDATE users SET {assignments}, UpdatedOn = $updatedOn WHERE Id = $id RETURNING {Columns}";
        command.Parameters.AddRange(parameters);
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$updatedOn", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadUser(reader) : null;
    }

    private static User ReadUser(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        FirstName = reader.GetString(1),
        LastName = reader.GetString(2),
        Email = reader.GetString(3),
        CreatedOn = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        UpdatedOn = DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        PasswordHash = reader.GetString(6),
        Role = reader.GetString(7),
        ImageUrl = reader.IsDBNull(8) ? null : reader.GetString(8)
    };
}
