using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;
using UserManagement.DataAccess.Database;
using UserManagement.DataAccess.Repositories;

namespace UserManagement.UnitTests.Database;

public sealed class SqliteUserRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "user-management-sqlite-" + Guid.NewGuid().ToString("N"));
    private SqliteUserRepository Repository() => new(new SqliteDatabase(Options.Create(new SqliteSettings { DatabasePath = Path.Combine(_directory, "users.db") })));
    private static User NewUser(string email = "user@example.com") => new()
    {
        FirstName = "Test",
        LastName = "User",
        Email = email,
        PasswordHash = "test-hash",
        Role = "User",
        CreatedOn = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedOn = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task CreateAndReopen_PreservesDataAndExistingIdFormat()
    {
        var user = NewUser();
        await Repository().CreateAsync(user);

        var repository = Repository();
        var result = await repository.GetByIdAsync(user.Id);

        Assert.Matches("^[0-9a-f]{24}$", user.Id);
        Assert.NotNull(result);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.PasswordHash, result.PasswordHash);
        Assert.Equal(user.CreatedOn, result.CreatedOn);
        Assert.Equal(DateTimeKind.Utc, result.CreatedOn.Kind);
        Assert.Single(await repository.GetUsersAsync());
        Assert.Equal(user.Id, (await repository.GetByEmailAsync("USER@example.com"))!.Id);
    }

    [Fact]
    public async Task EmailIndex_RejectsDuplicateIgnoringCase()
    {
        var repository = Repository();
        await repository.CreateAsync(NewUser());

        var error = await Assert.ThrowsAsync<SqliteException>(() => repository.CreateAsync(NewUser("USER@example.com")));

        Assert.Equal(19, error.SqliteErrorCode);
        Assert.Single(await repository.GetUsersAsync());
    }

    [Fact]
    public async Task ProfileAndImageChanges_ReturnPersistedUser()
    {
        var repository = Repository();
        var user = NewUser();
        await repository.CreateAsync(user);

        var profile = await repository.UpdateProfileAsync(user.Id, "Updated", "Name");
        Assert.Equal("Updated", profile!.FirstName);
        Assert.Equal("Name", profile.LastName);
        Assert.True(profile.UpdatedOn > user.UpdatedOn);

        var image = await repository.UpdateImageUrlAsync(user.Id, "profile-images/photo.png");
        Assert.Equal("profile-images/photo.png", image!.ImageUrl);

        var removed = await repository.RemoveImageUrlAsync(user.Id);
        Assert.Null(removed!.ImageUrl);
        Assert.Null((await Repository().GetByIdAsync(user.Id))!.ImageUrl);
    }

    [Fact]
    public async Task MissingUsers_ReturnNullWithoutChangingRows()
    {
        var repository = Repository();
        Assert.Null(await repository.GetByIdAsync("missing"));
        Assert.Null(await repository.GetByEmailAsync("missing@example.com"));
        Assert.Null(await repository.UpdateProfileAsync("missing", "First", "Last"));
        Assert.Null(await repository.UpdateImageUrlAsync("missing", "image.png"));
        Assert.Null(await repository.RemoveImageUrlAsync("missing"));
        Assert.Empty(await repository.GetUsersAsync());
    }

    [Fact]
    public async Task SqlInput_IsTreatedAsData()
    {
        var repository = Repository();
        var user = NewUser();
        await repository.CreateAsync(user);
        Assert.Null(await repository.GetByEmailAsync("' OR 1=1 --"));

        var updated = await repository.UpdateProfileAsync(user.Id, "'; DROP TABLE users; --", "Last");

        Assert.Equal("'; DROP TABLE users; --", updated!.FirstName);
        Assert.Single(await repository.GetUsersAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
