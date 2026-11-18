using Tagger.Infrastructure.Artwork;
using Xunit;

namespace Tagger.Infrastructure.Tests;

public sealed class ImageInspectorTests
{
    private readonly ImageInspector _sut = new();

    [Fact]
    public void Inspect_ShouldReadPngDimensions_WhenHeaderIsValid()
    {
        // Arrange
        var data = new byte[24];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(data, 0);
        WriteBigEndian(data, 16, 600);
        WriteBigEndian(data, 20, 400);

        // Act
        var result = _sut.Inspect(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("image/png", result.MimeType);
        Assert.Equal(600, result.Width);
        Assert.Equal(400, result.Height);
    }

    [Fact]
    public void Inspect_ShouldReadGifDimensions_WhenHeaderIsValid()
    {
        // Arrange
        var data = new byte[10];
        "GIF89a"u8.CopyTo(data);
        data[6] = 0x20;
        data[7] = 0x03;
        data[8] = 0x58;
        data[9] = 0x02;

        // Act
        var result = _sut.Inspect(data);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("image/gif", result.MimeType);
        Assert.Equal(800, result.Width);
        Assert.Equal(600, result.Height);
    }

    [Fact]
    public void Inspect_ShouldReturnNull_WhenBytesAreNotAnImage()
    {
        // Arrange
        var data = "not an image"u8.ToArray();

        // Act
        var result = _sut.Inspect(data);

        // Assert
        Assert.Null(result);
    }

    private static void WriteBigEndian(byte[] data, int offset, int value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
}
