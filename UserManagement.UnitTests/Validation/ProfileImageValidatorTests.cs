using UserManagement.Web.Validation;

namespace UserManagement.UnitTests.Validation;

public class ProfileImageValidatorTests
{
    [Theory]
    [InlineData("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0x00 })]
    [InlineData("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    public async Task HasExpectedSignatureAsync_ValidImageSignature_ReturnsTrue(string contentType, byte[] content)
    {
        await using var stream = new MemoryStream(content);

        var result = await ProfileImageValidator.HasExpectedSignatureAsync(stream, contentType, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task HasExpectedSignatureAsync_MismatchedImageSignature_ReturnsFalse()
    {
        await using var stream = new MemoryStream([0xFF, 0xD8, 0xFF, 0x00]);

        var result = await ProfileImageValidator.HasExpectedSignatureAsync(stream, "image/png", CancellationToken.None);

        Assert.False(result);
        Assert.Equal(0, stream.Position);
    }
}
