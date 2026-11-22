using NSubstitute;
using Tagger.Application.Playback;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Application.Tests.Playback;

public sealed class PlaybackServiceTests
{
    private readonly IAudioPlayer _player = Substitute.For<IAudioPlayer>();
    private readonly PlaybackService _sut;
    private readonly List<Track> _tracks =
    [
        TestTracks.Create("one.mp3"),
        TestTracks.Create("two.mp3"),
        TestTracks.Create("three.mp3")
    ];

    public PlaybackServiceTests()
    {
        _player.PlayAsync(Arg.Any<PlaybackRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        _sut = new PlaybackService(_player);
    }

    [Fact]
    public async Task PlayAsync_ShouldOpenSelectedTrack_WhenQueueStartsInMiddle()
    {
        // Arrange

        // Act
        await _sut.PlayAsync(_tracks, 1);

        // Assert
        await _player.Received(1).PlayAsync(
            Arg.Is<PlaybackRequest>(request => request.FilePath == _tracks[1].FilePath),
            Arg.Any<CancellationToken>());
        Assert.Same(_tracks[1], _sut.CurrentTrack);
        Assert.Equal(PlaybackState.Playing, _sut.Snapshot.State);
    }

    [Fact]
    public async Task PlayAsync_ShouldStopPlayer_WhenQueueIsEmpty()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 0);
        _player.ClearReceivedCalls();

        // Act
        await _sut.PlayAsync([], 0);

        // Assert
        _player.Received(1).Stop();
        Assert.Equal(PlaybackState.Idle, _sut.Snapshot.State);
        Assert.Null(_sut.CurrentTrack);
    }

    [Fact]
    public async Task PlayAsync_ShouldFail_WhenPlayerRejectsMedia()
    {
        // Arrange
        _player.PlayAsync(Arg.Any<PlaybackRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        // Act
        await _sut.PlayAsync(_tracks, 0);

        // Assert
        Assert.Equal(PlaybackState.Failed, _sut.Snapshot.State);
        Assert.NotNull(_sut.Snapshot.Error);
    }

    [Fact]
    public async Task ToggleAsync_ShouldPauseAndResume_WhenTrackIsPlaying()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 0);

        // Act
        await _sut.ToggleAsync();

        // Assert
        Assert.Equal(PlaybackState.Paused, _sut.Snapshot.State);
        _player.Received(1).Pause();

        // Act
        await _sut.ToggleAsync();

        // Assert
        Assert.Equal(PlaybackState.Playing, _sut.Snapshot.State);
        _player.Received(1).Resume();
    }

    [Fact]
    public async Task NextAsync_ShouldAdvance_WhenQueueHasNext()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 0);

        // Act
        await _sut.NextAsync();

        // Assert
        Assert.Equal(_tracks[1].FilePath, _sut.CurrentTrack!.FilePath);
        await _player.Received(1).PlayAsync(
            Arg.Is<PlaybackRequest>(request => request.FilePath == _tracks[1].FilePath),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NextAsync_ShouldStop_WhenAtEndAndRepeatOff()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, _tracks.Count - 1);
        _player.ClearReceivedCalls();

        // Act
        await _sut.NextAsync();

        // Assert
        Assert.Equal(PlaybackState.Stopped, _sut.Snapshot.State);
        await _player.DidNotReceive().PlayAsync(Arg.Any<PlaybackRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NextAsync_ShouldWrap_WhenAtEndAndRepeatAll()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, _tracks.Count - 1);
        _sut.SetRepeat(RepeatMode.All);

        // Act
        await _sut.NextAsync();

        // Assert
        Assert.Equal(_tracks[0].FilePath, _sut.CurrentTrack!.FilePath);
    }

    [Fact]
    public async Task PreviousAsync_ShouldRestart_WhenPositionBeyondThreshold()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 1);
        _player.SnapshotChanged += Raise.Event<EventHandler<PlaybackSnapshot>>(
            _player,
            new PlaybackSnapshot(PlaybackState.Playing, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(3), 1d, false, _tracks[1].FilePath, null));
        _player.ClearReceivedCalls();

        // Act
        await _sut.PreviousAsync();

        // Assert
        _player.Received(1).Seek(TimeSpan.Zero);
        await _player.DidNotReceive().PlayAsync(Arg.Any<PlaybackRequest>(), Arg.Any<CancellationToken>());
        Assert.Equal(_tracks[1].FilePath, _sut.CurrentTrack!.FilePath);
    }

    [Fact]
    public async Task HandleEnded_ShouldAdvance_WhenRepeatOffAndNextExists()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 0);
        _player.ClearReceivedCalls();

        // Act
        await _sut.HandleEndedAsync(_tracks[0].FilePath);

        // Assert
        Assert.Equal(_tracks[1].FilePath, _sut.CurrentTrack!.FilePath);
        await _player.Received(1).PlayAsync(
            Arg.Is<PlaybackRequest>(request => request.FilePath == _tracks[1].FilePath),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleEnded_ShouldReplay_WhenRepeatOne()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 0);
        _sut.SetRepeat(RepeatMode.One);
        _player.ClearReceivedCalls();

        // Act
        await _sut.HandleEndedAsync(_tracks[0].FilePath);

        // Assert
        Assert.Equal(_tracks[0].FilePath, _sut.CurrentTrack!.FilePath);
        await _player.Received(1).PlayAsync(
            Arg.Is<PlaybackRequest>(request => request.FilePath == _tracks[0].FilePath),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleEnded_ShouldNotAdvance_WhenTrackChangedMeanwhile()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 1);
        _player.ClearReceivedCalls();

        // Act
        await _sut.HandleEndedAsync(_tracks[0].FilePath);

        // Assert
        await _player.DidNotReceive().PlayAsync(Arg.Any<PlaybackRequest>(), Arg.Any<CancellationToken>());
        Assert.Equal(_tracks[1].FilePath, _sut.CurrentTrack!.FilePath);
    }

    [Fact]
    public async Task Stop_ShouldNotAutoAdvance_WhenEndArrivesAfterStop()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 0);
        _sut.Stop();
        _player.ClearReceivedCalls();

        // Act
        await _sut.HandleEndedAsync(_tracks[0].FilePath);

        // Assert
        Assert.Equal(PlaybackState.Stopped, _sut.Snapshot.State);
        await _player.DidNotReceive().PlayAsync(Arg.Any<PlaybackRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayerFailure_ShouldExposeFailureAndKeepCurrent_WhenMediaFails()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 1);
        PlaybackFailure? reported = null;
        _sut.Failed += (_, failure) => reported = failure;

        // Act
        _player.Failed += Raise.Event<EventHandler<PlaybackFailure>>(
            _player,
            new PlaybackFailure(_tracks[1].FilePath, "unsupported codec"));

        // Assert
        Assert.Equal(PlaybackState.Failed, _sut.Snapshot.State);
        Assert.Equal("unsupported codec", reported?.Message);
        Assert.Equal(_tracks[1].FilePath, _sut.CurrentTrack!.FilePath);
    }

    [Fact]
    public void SetVolume_ShouldClampAndForward_WhenOutOfRange()
    {
        // Arrange

        // Act
        _sut.SetVolume(2d);
        _sut.SetVolume(-1d);

        // Assert
        _player.Received(1).SetVolume(1d);
        _player.Received(1).SetVolume(0d);
        Assert.Equal(0d, _sut.Snapshot.Volume);
    }

    [Fact]
    public async Task PrepareForMutation_ShouldStopAndCapturePosition_WhenCurrentTrackIsMutated()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 1);
        _player.SnapshotChanged += Raise.Event<EventHandler<PlaybackSnapshot>>(
            _player,
            new PlaybackSnapshot(
                PlaybackState.Playing,
                TimeSpan.FromSeconds(12),
                TimeSpan.FromMinutes(3),
                1d,
                false,
                _tracks[1].FilePath,
                null));

        // Act
        var continuation = _sut.PrepareForMutation([_tracks[1].FilePath]);

        // Assert
        Assert.NotNull(continuation);
        Assert.Equal(_tracks[1].Id, continuation.TrackId);
        Assert.Equal(TimeSpan.FromSeconds(12), continuation.Position);
        Assert.True(continuation.WasPlaying);
        _player.Received(1).Stop();
        Assert.Equal(PlaybackState.Stopped, _sut.Snapshot.State);
    }

    [Fact]
    public async Task PrepareForMutation_ShouldLeavePlaybackAlone_WhenAnotherTrackIsMutated()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 1);
        _player.ClearReceivedCalls();

        // Act
        var continuation = _sut.PrepareForMutation([_tracks[0].FilePath]);

        // Assert
        Assert.Null(continuation);
        _player.DidNotReceive().Stop();
        Assert.Equal(PlaybackState.Playing, _sut.Snapshot.State);
    }

    [Fact]
    public async Task ResumeAfterMutationAsync_ShouldRestartRenamedTrackAtPosition_WhenItWasPlaying()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 1);
        var renamed = _tracks[1] with
        {
            FilePath = Path.Combine("music", "renamed.mp3"),
            FileName = "renamed.mp3"
        };
        var continuation = new PlaybackContinuation(
            _tracks[1].Id,
            _tracks[1].FilePath,
            TimeSpan.FromSeconds(30),
            true,
            1);
        _player.ClearReceivedCalls();

        // Act
        await _sut.ResumeAfterMutationAsync(continuation, renamed);

        // Assert
        Assert.Equal(renamed.FilePath, _sut.CurrentTrack!.FilePath);
        await _player.Received(1).PlayAsync(
            Arg.Is<PlaybackRequest>(request =>
                request.FilePath == renamed.FilePath &&
                request.StartPosition == TimeSpan.FromSeconds(30)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeAfterMutationAsync_ShouldNotRestart_WhenPlaybackWasPaused()
    {
        // Arrange
        var continuation = new PlaybackContinuation(
            _tracks[0].Id,
            _tracks[0].FilePath,
            TimeSpan.FromSeconds(30),
            false,
            0);

        // Act
        await _sut.ResumeAfterMutationAsync(continuation);

        // Assert
        await _player.DidNotReceive().PlayAsync(
            Arg.Any<PlaybackRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveFromQueue_ShouldStop_WhenCurrentTrackIsRemoved()
    {
        // Arrange
        await _sut.PlayAsync(_tracks, 1);

        // Act
        _sut.RemoveFromQueue([_tracks[1].FilePath]);

        // Assert
        Assert.Null(_sut.CurrentTrack);
        Assert.Equal(PlaybackState.Stopped, _sut.Snapshot.State);
        _player.Received().Stop();
    }
}
