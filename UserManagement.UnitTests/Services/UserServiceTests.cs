using Moq;
using UserManagement.Application.Interfaces;
using UserManagement.Application.Services;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.UnitTests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly Mock<IFileStorageService> _fileStorage = new();

    private UserService CreateService()
    {
        return new UserService(_repository.Object, _fileStorage.Object);
    }

    //  GET /user/id
    [Fact]
    public async Task GetUserByIdAsync_UserHasImage_ReturnsPublicImageUrl()
    {
        var user = new User { Id = "user-id", FirstName = "Henry", LastName = "Lee", Email = "henry@example.com", ImageUrl = "profile-images/user-id/photo.png" };

        _repository.Setup(repository => repository.GetByIdAsync("user-id")).ReturnsAsync(user);
        _fileStorage.Setup(storage => storage.GetUrl(user.ImageUrl)).Returns("https://images.example.com/profile-images/user-id/photo.png");

        var result = await CreateService().GetUserByIdAsync("user-id");

        Assert.NotNull(result);
        Assert.Equal("user-id", result.Id);
        Assert.Equal("https://images.example.com/profile-images/user-id/photo.png", result.ImageUrl);
    }

    //  GET /users
    [Fact]
    public async Task GetUsersAsync_UsersExist_ReturnsUsers()
    {
        var users = new List<User>
        {
            new() { Id = "user-id", FirstName = "Henry", LastName = "Lee", Email = "henry@example.com" },
            new() { Id = "admin-id", FirstName = "Admin", LastName = "User", Email = "admin@example.com" }
        };

        _repository.Setup(repository => repository.GetUsersAsync()).ReturnsAsync(users);

        var result = await CreateService().GetUsersAsync();

        Assert.Equal(2, result.Count);
    }

    //  PUT /user/id 
    [Fact]
    public async Task UpdateProfileAsync_ValidName_UpdatesAndReturnsUser()
    {
        var request = new UpdateUserProfileRequest { FirstName = " Henry ", LastName = " Lee " };
        var updatedUser = new User { Id = "user-id", FirstName = "Henry", LastName = "Lee", Email = "henry@example.com" };

        _repository.Setup(repository => repository.UpdateProfileAsync("user-id", "Henry", "Lee")).ReturnsAsync(updatedUser);

        var result = await CreateService().UpdateProfileAsync("user-id", request);

        Assert.NotNull(result);
        Assert.Equal("Henry", result.FirstName);
        Assert.Equal("Lee", result.LastName);
    }

    // PUT /user/id/profile-picture
    [Fact]
    public async Task UpdateImageUrlAsync_UploadsImage()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var existingUser = new User { Id = "user-id", ImageUrl = "old-image.png" };
        var updatedUser = new User { Id = "user-id", ImageUrl = "new-image.png" };

        _repository.Setup(repository => repository.GetByIdAsync("user-id")).ReturnsAsync(existingUser);
        _repository.Setup(repository => repository.UpdateImageUrlAsync("user-id", It.IsAny<string>())).ReturnsAsync(updatedUser);
        _fileStorage.Setup(storage => storage.GetUrl("new-image.png")).Returns("https://images.example.com/new-image.png");

        var result = await CreateService().UpdateImageUrlAsync("user-id", stream, ".png", "image/png");

        Assert.NotNull(result);
        _fileStorage.Verify(storage => storage.UploadAsync(stream, It.IsAny<string>(), "image/png", It.IsAny<CancellationToken>()), Times.Once);
    }

    // DELETE /user/id/profile-picture
    [Fact]
    public async Task RemoveImageUrlAsync_DeletesImage()
    {
        var existingUser = new User { Id = "user-id", ImageUrl = "image.png" };
        var updatedUser = new User { Id = "user-id" };

        _repository.Setup(repository => repository.GetByIdAsync("user-id")).ReturnsAsync(existingUser);
        _repository.Setup(repository => repository.RemoveImageUrlAsync("user-id")).ReturnsAsync(updatedUser);

        var result = await CreateService().RemoveImageUrlAsync("user-id");

        Assert.NotNull(result);
        _fileStorage.Verify(storage => storage.DeleteAsync("image.png", It.IsAny<CancellationToken>()), Times.Once);
    }
}
