using Tagger.Infrastructure.Files;
using Xunit;

namespace Tagger.Infrastructure.Tests;

public sealed class AudioFileScannerTests
{
    [Theory]
    [InlineData("song.mp3", true)]
    [InlineData("song.FLAC", true)]
    [InlineData("song.m4a", true)]
    [InlineData("song.opus", true)]
    [InlineData("cover.jpg", false)]
    [InlineData("notes.txt", false)]
    public void IsSupported_ShouldMatchExtension_WhenFileIsChecked(string path, bool expected)
    {
        // Arrange
        var _sut = AudioFileScanner.IsSupported;

        // Act
        var supported = _sut(path);

        // Assert
        Assert.Equal(expected, supported);
    }
}
