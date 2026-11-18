using Tagger.Application.Duplicates;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Application.Tests;

public sealed class DuplicateFinderTests
{
    private readonly DuplicateFinder _sut = new();

    [Fact]
    public void Find_ShouldGroupTracks_WhenArtistAndTitleMatch()
    {
        // Arrange
        var tracks = new[]
        {
            TestTracks.Create("one.mp3", title: "Northern Lights", artist: "Aurora"),
            TestTracks.Create("two.mp3", title: "northern lights", artist: "aurora"),
            TestTracks.Create("three.mp3", title: "Ember & Ash", artist: "Aurora")
        };

        // Act
        var groups = _sut.Find(tracks, DuplicateMatchMode.ArtistAndTitle);

        // Assert
        Assert.Single(groups);
        Assert.Equal(2, groups[0].Count);
    }

    [Fact]
    public void Find_ShouldIgnoreTracks_WhenTitleOrArtistIsMissing()
    {
        // Arrange
        var tracks = new[]
        {
            TestTracks.Create("one.mp3", title: null, artist: "Aurora"),
            TestTracks.Create("two.mp3", title: "Song", artist: null)
        };

        // Act
        var groups = _sut.Find(tracks, DuplicateMatchMode.ArtistAndTitle);

        // Assert
        Assert.Empty(groups);
    }

    [Fact]
    public void Find_ShouldGroupByAlbumAndTrack_WhenModeIsAlbumAndTrack()
    {
        // Arrange
        var first = TestTracks.Create("one.mp3", album: "Static Bloom");
        var second = TestTracks.Create("two.mp3", album: "Static Bloom");
        var tracks = new[]
        {
            first with { Tags = first.Tags.With(TagField.Track, "1").With(TagField.Disc, "1") },
            second with { Tags = second.Tags.With(TagField.Track, "1").With(TagField.Disc, "1") }
        };

        // Act
        var groups = _sut.Find(tracks, DuplicateMatchMode.AlbumAndTrack);

        // Assert
        Assert.Single(groups);
    }
}
