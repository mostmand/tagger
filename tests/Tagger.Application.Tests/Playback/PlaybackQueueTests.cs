using Tagger.Application.Playback;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Application.Tests.Playback;

public sealed class PlaybackQueueTests
{
    private readonly List<Track> _tracks =
    [
        TestTracks.Create("one.mp3"),
        TestTracks.Create("two.mp3"),
        TestTracks.Create("three.mp3")
    ];

    [Fact]
    public void MoveNext_ShouldReturnNextTrack_WhenQueueHasNext()
    {
        // Arrange
        var _sut = new PlaybackQueue(_tracks);

        // Act
        var next = _sut.MoveNext(RepeatMode.Off);

        // Assert
        Assert.NotNull(next);
        Assert.Equal(_tracks[1].FilePath, next.FilePath);
        Assert.Equal(1, _sut.CurrentIndex);
    }

    [Fact]
    public void MoveNext_ShouldReturnNull_WhenAtEndAndRepeatOff()
    {
        // Arrange
        var _sut = new PlaybackQueue(_tracks, _tracks.Count - 1);

        // Act
        var next = _sut.MoveNext(RepeatMode.Off);

        // Assert
        Assert.Null(next);
        Assert.Equal(_tracks.Count - 1, _sut.CurrentIndex);
    }

    [Fact]
    public void MoveNext_ShouldWrapToFirst_WhenAtEndAndRepeatAll()
    {
        // Arrange
        var _sut = new PlaybackQueue(_tracks, _tracks.Count - 1);

        // Act
        var next = _sut.MoveNext(RepeatMode.All);

        // Assert
        Assert.NotNull(next);
        Assert.Equal(_tracks[0].FilePath, next.FilePath);
        Assert.Equal(0, _sut.CurrentIndex);
    }

    [Fact]
    public void MovePrevious_ShouldReturnPreviousTrack_WhenQueueHasPrevious()
    {
        // Arrange
        var _sut = new PlaybackQueue(_tracks, 2);

        // Act
        var previous = _sut.MovePrevious(RepeatMode.Off);

        // Assert
        Assert.NotNull(previous);
        Assert.Equal(_tracks[1].FilePath, previous.FilePath);
    }

    [Fact]
    public void MovePrevious_ShouldWrapToLast_WhenAtStartAndRepeatAll()
    {
        // Arrange
        var _sut = new PlaybackQueue(_tracks);

        // Act
        var previous = _sut.MovePrevious(RepeatMode.All);

        // Assert
        Assert.NotNull(previous);
        Assert.Equal(_tracks[^1].FilePath, previous.FilePath);
    }

    [Fact]
    public void RemovePaths_ShouldKeepCurrentTrack_WhenAnEarlierTrackIsRemoved()
    {
        // Arrange
        var _sut = new PlaybackQueue(_tracks, 1);

        // Act
        _sut.RemovePaths([_tracks[0].FilePath]);

        // Assert
        Assert.NotNull(_sut.Current);
        Assert.Equal(_tracks[1].FilePath, _sut.Current.FilePath);
        Assert.Equal(0, _sut.CurrentIndex);
        Assert.Equal(2, _sut.Count);
    }

    [Fact]
    public void RemovePaths_ShouldClearCurrent_WhenCurrentTrackIsRemoved()
    {
        // Arrange
        var _sut = new PlaybackQueue(_tracks, 1);

        // Act
        _sut.RemovePaths([_tracks[1].FilePath]);

        // Assert
        Assert.Null(_sut.Current);
        Assert.Equal(-1, _sut.CurrentIndex);
        Assert.Equal(2, _sut.Count);
    }

    [Fact]
    public void UpdateTrack_ShouldReplaceItem_WhenPathMatches()
    {
        // Arrange
        var _sut = new PlaybackQueue(_tracks);
        var updated = _tracks[0] with { FileSizeBytes = 2048 };

        // Act
        _sut.UpdateTrack(updated);

        // Assert
        Assert.Same(updated, _sut.Current);
    }

    [Fact]
    public void UpdateTrack_ShouldReplaceRenamedItem_WhenPreviousPathMatches()
    {
        // Arrange
        var _sut = new PlaybackQueue(_tracks);
        var renamed = _tracks[0].WithFilePath(Path.Combine("music", "renamed.mp3"));

        // Act
        _sut.UpdateTrack(renamed, _tracks[0].FilePath);

        // Assert
        Assert.Same(renamed, _sut.Current);
        Assert.Equal(renamed.FilePath, _sut.Current!.FilePath);
    }
}
