using UserManagement.Application.Interfaces;

namespace UserManagement.Web.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _uploadsRoot;

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        var webRoot = environment.WebRootPath
            ?? Path.Combine(environment.ContentRootPath, "wwwroot");

        _uploadsRoot = Path.Combine(webRoot, "uploads");
    }

    public async Task UploadAsync(
        Stream stream,
        string fileKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var relativePath = fileKey.Replace(
            '/',
            Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(
            Path.Combine(_uploadsRoot, relativePath));

        var allowedRoot =
            Path.GetFullPath(_uploadsRoot) +
            Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
                allowedRoot,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid file path.");
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)!);

        await using var output = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        await stream.CopyToAsync(output, cancellationToken);
    }

    public Task DeleteAsync(
        string fileKey,
        CancellationToken cancellationToken = default)
    {
        var relativePath = fileKey.Replace(
            '/',
            Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(
            Path.Combine(_uploadsRoot, relativePath));

        var allowedRoot =
            Path.GetFullPath(_uploadsRoot) +
            Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
                allowedRoot,
                StringComparison.Ordinal))
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