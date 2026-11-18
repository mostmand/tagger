using NSubstitute;
using Tagger.Application.Abstractions;
using Tagger.Application.Library;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Application.Tests;

public sealed class SelectionSummaryServiceTests
{
    private readonly SelectionSummaryService _sut = new();

    [Fact]
    public void Summarize_ShouldExposeCommonValue_WhenAllTracksShareIt()
    {
        // Arrange
        var tracks = new[]
        {
            TestTracks.Create("one.mp3", artist: "Aurora"),
            TestTracks.Create("two.mp3", artist: "Aurora")
        };

        // Act
        var result = _sut.Summarize(tracks);

        // Assert
        var artist = result.For(TagField.Artist);
        Assert.Equal("Aurora", artist.CommonValue);
        Assert.False(artist.IsMixed);
        Assert.Equal(1, artist.ValueCount);
    }

    [Fact]
    public void Summarize_ShouldMarkMixed_WhenValuesDiffer()
    {
        // Arrange
        var tracks = new[]
        {
            TestTracks.Create("one.mp3", artist: "Aurora"),
            TestTracks.Create("two.mp3", artist: "Someone Else")
        };

        // Act
        var result = _sut.Summarize(tracks);

        // Assert
        var artist = result.For(TagField.Artist);
        Assert.True(artist.IsMixed);
        Assert.Null(artist.CommonValue);
        Assert.True(result.HasMixedValues);
    }

    [Fact]
    public void Summarize_ShouldReportMixedArtwork_WhenOnlyOneTrackHasArtwork()
    {
        // Arrange
        var artwork = new CoverArt([1, 2, 3], "image/png");
        var tracks = new[]
        {
            TestTracks.Create("one.mp3", artwork: artwork),
            TestTracks.Create("two.mp3")
        };

        // Act
        var result = _sut.Summarize(tracks);

        // Assert
        Assert.True(result.IsCoverArtMixed);
        Assert.Null(result.CommonCoverArt);
    }

    [Fact]
    public void Summarize_ShouldReturnEmpty_WhenSelectionIsEmpty()
    {
        // Arrange

        // Act
        var result = _sut.Summarize([]);

        // Assert
        Assert.Same(SelectionSummary.Empty, result);
    }
}
