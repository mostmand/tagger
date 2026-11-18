using Tagger.Domain.Tracks;
using Tagger.Desktop.ViewModels;
using Xunit;

namespace Tagger.Desktop.Tests;

public sealed class TrackRowViewModelTests
{
    private readonly TrackRowViewModel _sut;

    public TrackRowViewModelTests()
    {
        _sut = new TrackRowViewModel(
            TestTrack.Create("song.mp3", title: "Northern Lights", artist: "Aurora", year: "2021"),
            3);
    }

    [Fact]
    public void Properties_ShouldExposeDisplayValues_WhenConstructed()
    {
        // Arrange

        // Act
        var title = _sut.Title;
        var artist = _sut.Artist;
        var index = _sut.Index;

        // Assert
        Assert.Equal("Northern Lights", title);
        Assert.Equal("Aurora", artist);
        Assert.Equal(3, index);
        Assert.Equal("MP3", _sut.Format);
    }

    [Fact]
    public void Update_ShouldReplaceTrackValues_WhenNewTrackProvided()
    {
        // Arrange
        var updated = TestTrack.Create("song.mp3", title: "Midnight Run", artist: "Neon Garden", year: "2019");

        // Act
        _sut.Update(updated);

        // Assert
        Assert.Equal("Midnight Run", _sut.Title);
        Assert.Equal("Neon Garden", _sut.Artist);
        Assert.Equal("2019", _sut.Year);
    }
}

public sealed class ChangePreviewViewModelTests
{
    [Fact]
    public void Constructor_ShouldFlattenDiffs_WhenPreviewHasChanges()
    {
        // Arrange
        var track = TestTrack.Create("song.mp3", title: "Old");
        var preview = new ChangePreview(
        [
            new TrackChangePreview(
                track,
                track.Tags.With(TagField.Title, "New"),
                [new TagFieldDiff(TagField.Title, "Old", "New")],
                false,
                true)
        ]);

        // Act
        var _sut = new ChangePreviewViewModel(preview, "Preview");

        // Assert
        Assert.Single(_sut.Rows);
        Assert.Equal("Title", _sut.Rows[0].Field);
        Assert.Equal("Old", _sut.Rows[0].Before);
        Assert.Equal("New", _sut.Rows[0].After);
        Assert.Contains("1 track", _sut.Summary);
    }

    [Fact]
    public void Constructor_ShouldAddArtworkRow_WhenArtworkChanged()
    {
        // Arrange
        var track = TestTrack.Create("song.mp3");
        var preview = new ChangePreview(
        [
            new TrackChangePreview(track, track.Tags, [], true, true)
        ]);

        // Act
        var _sut = new ChangePreviewViewModel(preview, "Preview");

        // Assert
        Assert.Single(_sut.Rows);
        Assert.Equal("Artwork", _sut.Rows[0].Field);
    }
}
