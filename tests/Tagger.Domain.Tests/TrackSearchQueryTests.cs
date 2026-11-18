using Tagger.Domain.Searching;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Domain.Tests;

public sealed class TrackSearchQueryTests
{
    private readonly Track _track;

    public TrackSearchQueryTests()
    {
        _track = new Track
        {
            Id = Guid.NewGuid(),
            FilePath = Path.Combine("music", "Aurora", "Northern Lights.flac"),
            FileName = "Northern Lights.flac",
            Extension = "flac",
            Tags = new TrackTags(
            [
                new(TagField.Artist, "Aurora"),
                new(TagField.Album, "Static Bloom"),
                new(TagField.Title, "Northern Lights"),
                new(TagField.Year, "2021"),
                new(TagField.Genre, "Synthwave")
            ])
        };
    }

    [Fact]
    public void Matches_ShouldFindFreeText_WhenTitleContainsIt()
    {
        // Arrange
        var _sut = TrackSearchQuery.Parse("northern");

        // Act
        var matches = _sut.Matches(_track);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void Matches_ShouldFilterByField_WhenQualifierIsUsed()
    {
        // Arrange
        var _sut = TrackSearchQuery.Parse("artist:aurora");

        // Act
        var matches = _sut.Matches(_track);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void Matches_ShouldRejectTrack_WhenFieldQualifierDiffers()
    {
        // Arrange
        var _sut = TrackSearchQuery.Parse("artist:someone");

        // Act
        var matches = _sut.Matches(_track);

        // Assert
        Assert.False(matches);
    }

    [Fact]
    public void Matches_ShouldApplyYearComparison_WhenGreaterThanUsed()
    {
        // Arrange
        var _sut = TrackSearchQuery.Parse("year:>=2020");

        // Act
        var matches = _sut.Matches(_track);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void Matches_ShouldFilterByExtension_WhenExtUsed()
    {
        // Arrange
        var _sut = TrackSearchQuery.Parse("ext:flac");

        // Act
        var matches = _sut.Matches(_track);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void Matches_ShouldKeepQuotedPhraseTogether_WhenSearching()
    {
        // Arrange
        var _sut = TrackSearchQuery.Parse("\"northern lights\"");

        // Act
        var matches = _sut.Matches(_track);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void IsEmpty_ShouldBeTrue_WhenQueryIsWhitespace()
    {
        // Arrange
        var _sut = TrackSearchQuery.Parse("   ");

        // Act
        var empty = _sut.IsEmpty;

        // Assert
        Assert.True(empty);
    }
}
