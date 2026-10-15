namespace UserManagement.Application.Interfaces;

public interface IFileStorageService
{
    Task UploadAsync( Stream stream, string fileKey, string contentType, CancellationToken cancellationToken = default);

    Task DeleteAsync(  string fileKey, CancellationToken cancellationToken = default);

    string GetUrl(string fileKey);
}