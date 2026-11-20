using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using UserManagement.Core.Settings;
using UserManagement.Web.Services;

namespace UserManagement.UnitTests.Services;

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "user-management-storage-" + Guid.NewGuid().ToString("N"));

    private LocalFileStorageService CreateService(string directory)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.ContentRootPath).Returns(_root);
        return new LocalFileStorageService(Options.Create(new AppPathsSettings { UploadDirectory = directory }), environment.Object);
    }

    [Fact]
    public async Task UploadAndDelete_UsesConfiguredDirectoryAndPreservesUrl()
    {
        var directory = Path.Combine(_root, "external-uploads");
        var service = CreateService(directory);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        await service.UploadAsync(stream, "profile-images/user/photo.png", "image/png");

        var image = Path.Combine(directory, "profile-images", "user", "photo.png");
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(image));
        Assert.Equal("/uploads/profile-images/user/photo.png", service.GetUrl("profile-images/user/photo.png"));
        Assert.False(Directory.Exists(Path.Combine(_root, "wwwroot")));

        await service.DeleteAsync("profile-images/user/photo.png");

        Assert.False(File.Exists(image));
    }

    [Fact]
    public void RelativeDirectory_ResolvesAgainstContentRoot()
    {
        var service = CreateService("custom-uploads");

        Assert.Equal(Path.Combine(_root, "custom-uploads"), service.UploadRoot);
        Assert.True(Directory.Exists(service.UploadRoot));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("../../outside.png")]
    public async Task UploadAndDelete_RejectEscapingConfiguredDirectory(string key)
    {
        var service = CreateService(Path.Combine(_root, "uploads"));
        using var stream = new MemoryStream(new byte[] { 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(stream, key, "image/png"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(key));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
