using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UserManagement.Application.Interfaces;
using UserManagement.Core.Settings;

namespace UserManagement.Web.Services;

public sealed class CloudflareR2StorageService
    : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly CloudflareR2Settings _settings;

    public CloudflareR2StorageService(
        IAmazonS3 s3Client,
        IOptions<CloudflareR2Settings> options)
    {
        _s3Client = s3Client;
        _settings = options.Value;
    }

    public async Task UploadAsync(
        Stream stream,
        string fileKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = fileKey,
            InputStream = stream,
            ContentType = contentType,

            // Required for Cloudflare R2 with AWSSDK.S3.
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };

        await _s3Client.PutObjectAsync(
            request,
            cancellationToken);
    }

    public async Task DeleteAsync(
        string fileKey,
        CancellationToken cancellationToken = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = fileKey
        };

        await _s3Client.DeleteObjectAsync(
            request,
            cancellationToken);
    }

    public string GetUrl(string fileKey)
    {
        return
            $"{_settings.PublicBaseUrl.TrimEnd('/')}/" +
            fileKey.TrimStart('/');
    }
}