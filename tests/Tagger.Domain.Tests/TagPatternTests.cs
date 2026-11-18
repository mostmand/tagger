using Tagger.Domain.Naming;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Domain.Tests;

public sealed class TagPatternTests
{
    private readonly Track _track;

    public TagPatternTests()
    {
        _track = new Track
        {
            Id = Guid.NewGuid(),
            FilePath = Path.Combine("music", "Aurora", "01 - Northern Lights.mp3"),
            FileName = "01 - Northern Lights.mp3",
            Extension = "mp3",
            Tags = new TrackTags(
            [
                new(TagField.Artist, "Aurora"),
                new(TagField.Album, "Static Bloom"),
                new(TagField.Title, "Northern Lights"),
                new(TagField.Track, "1")
            ])
        };
    }

    [Fact]
    public void Render_ShouldReplaceTokens_WhenTemplateHasFields()
    {
        // Arrange
        var _sut = TagPattern.Parse("%artist% - %title%");

        // Act
        var result = _sut.Render(_track);

        // Assert
        Assert.Equal("Aurora - Northern Lights", result);
    }

    [Fact]
    public void Render_ShouldZeroPadTrack_WhenTrack2TokenIsUsed()
    {
        // Arrange
        var _sut = TagPattern.Parse("%track2% - %title%");

        // Act
        var result = _sut.Render(_track);

        // Assert
        Assert.Equal("01 - Northern Lights", result);
    }

    [Fact]
    public void Render_ShouldUseSpecialTokens_WhenRequested()
    {
        // Arrange
        var _sut = TagPattern.Parse("%_directory%/%_filename%.%ext%");

        // Act
        var result = _sut.Render(_track);

        // Assert
        Assert.Equal("Aurora/01 - Northern Lights.mp3", result.Replace('\\', '/'));
    }

    [Fact]
    public void Parse_ShouldThrow_WhenTokenIsUnknown()
    {
        // Arrange
        var _sut = TagPattern.Parse;

        // Act
        var exception = Assert.Throws<FormatException>(() => _sut("%mystery%"));

        // Assert
        Assert.Contains("mystery", exception.Message);
    }

    [Fact]
    public void Parse_ShouldThrow_WhenTemplateHasNoToken()
    {
        // Arrange
        var _sut = TagPattern.Parse;

        // Act
        var exception = Assert.Throws<FormatException>(() => _sut("plain name"));

        // Assert
        Assert.Contains("field token", exception.Message);
    }
}
