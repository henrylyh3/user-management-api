using UserManagement.Application.Interfaces;
using UserManagement.Core.Settings;
using Microsoft.Extensions.Options;

namespace UserManagement.Web.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _uploadsRoot;

    public string UploadRoot => _uploadsRoot;

    public LocalFileStorageService(IOptions<AppPathsSettings> settings, IWebHostEnvironment environment)
    {
        var directory = settings.Value.UploadDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("AppPaths:UploadDirectory must be configured.");
        }
        _uploadsRoot = Path.GetFullPath(directory, environment.ContentRootPath);
        Directory.CreateDirectory(_uploadsRoot);
    }

    public async Task UploadAsync(Stream stream, string fileKey, string contentType, CancellationToken cancellationToken = default)
    {
        var relativePath = fileKey.Replace( '/', Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(Path.Combine(_uploadsRoot, relativePath));

        var allowedRoot = Path.GetFullPath(_uploadsRoot) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith( allowedRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid file path.");
        }

        Directory.CreateDirectory( Path.GetDirectoryName(fullPath)!);

        await using var output = new FileStream( fullPath, FileMode.Create, FileAccess.Write, FileShare.None);

        await stream.CopyToAsync(output, cancellationToken);
    }

    public Task DeleteAsync( string fileKey, CancellationToken cancellationToken = default)
    {
        var relativePath = fileKey.Replace( '/', Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath( Path.Combine(_uploadsRoot, relativePath));

        var allowedRoot = Path.GetFullPath(_uploadsRoot) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith( allowedRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid file path.");
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetUrl(string fileKey)
    {
        return $"/uploads/{fileKey.Replace("\\", "/")}";
    }
}