using Tagger.Domain.Naming;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Domain.Tests;

public sealed class FilenamePatternParserTests
{
    private readonly FilenamePatternParser _sut = new();

    [Fact]
    public void Parse_ShouldExtractFields_WhenFilenameMatchesPattern()
    {
        // Arrange
        var pattern = TagPattern.Parse("%artist% - %title%");

        // Act
        var result = _sut.Parse("Aurora - Northern Lights.mp3", pattern);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Aurora", result.Tags.Get(TagField.Artist));
        Assert.Equal("Northern Lights", result.Tags.Get(TagField.Title));
    }

    [Fact]
    public void Parse_ShouldExtractTrackAndTotal_WhenPatternHasBothTokens()
    {
        // Arrange
        var pattern = TagPattern.Parse("%track%-%tracktotal% - %title%");

        // Act
        var result = _sut.Parse("03-12 - Ember & Ash.mp3", pattern);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("03", result.Tags.Get(TagField.Track));
        Assert.Equal("12", result.Tags.Get(TagField.TrackTotal));
        Assert.Equal("Ember & Ash", result.Tags.Get(TagField.Title));
    }

    [Fact]
    public void Parse_ShouldFail_WhenFilenameDoesNotMatch()
    {
        // Arrange
        var pattern = TagPattern.Parse("%artist% - %title%");

        // Act
        var result = _sut.Parse("Loose Demo.mp3", pattern);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("does not match", result.Error);
    }

    [Fact]
    public void Parse_ShouldReadPaddedTrack_WhenPatternUsesTrack2()
    {
        // Arrange
        var pattern = TagPattern.Parse("%track2% %title%");

        // Act
        var result = _sut.Parse("07 Golden Hour.mp3", pattern);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("07", result.Tags.Get(TagField.Track));
        Assert.Equal("Golden Hour", result.Tags.Get(TagField.Title));
    }
}
