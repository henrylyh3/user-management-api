namespace UserManagement.Web.Validation;

public static class ProfileImageValidator
{
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static async Task<bool> HasExpectedSignatureAsync(Stream stream, string contentType, CancellationToken cancellationToken)
    {
        if (!stream.CanSeek)
        {
            return false;
        }

        var signature = contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ? JpegSignature
            : contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? PngSignature
            : null;

        if (signature is null)
        {
            return false;
        }

        var originalPosition = stream.Position;
        var header = new byte[signature.Length];

        try
        {
            var totalRead = 0;

            while (totalRead < header.Length)
            {
                var read = await stream.ReadAsync(header.AsMemory(totalRead), cancellationToken);

                if (read == 0)
                {
                    return false;
                }

                totalRead += read;
            }

            return header.AsSpan().SequenceEqual(signature);
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }
}
