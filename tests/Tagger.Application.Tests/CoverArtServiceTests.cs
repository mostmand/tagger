using NSubstitute;
using Tagger.Application.Abstractions;
using Tagger.Application.Artwork;
using Xunit;

namespace Tagger.Application.Tests;

public sealed class CoverArtServiceTests
{
    private readonly IImageInspector _inspector = Substitute.For<IImageInspector>();
    private readonly CoverArtService _sut;

    public CoverArtServiceTests()
    {
        _sut = new CoverArtService(_inspector);
    }

    [Fact]
    public void Create_ShouldReturnArtwork_WhenImageIsSupported()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3 };
        _inspector.Inspect(data).Returns(new ImageInfo("image/png", 600, 600));

        // Act
        var result = _sut.Create(data, "  Front cover  ");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Artwork);
        Assert.Equal("image/png", result.Artwork.MimeType);
        Assert.Equal("Front cover", result.Artwork.Description);
    }

    [Fact]
    public void Create_ShouldFail_WhenImageIsEmpty()
    {
        // Arrange

        // Act
        var result = _sut.Create([]);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_ShouldFail_WhenImageFormatIsUnsupported()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3 };
        _inspector.Inspect(data).Returns((ImageInfo?)null);

        // Act
        var result = _sut.Create(data);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unsupported", result.Error);
    }

    [Fact]
    public void Create_ShouldFail_WhenImageExceedsMaximumSize()
    {
        // Arrange
        var data = new byte[CoverArtService.MaxSizeBytes + 1];

        // Act
        var result = _sut.Create(data);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("20 MB", result.Error);
    }
}
