using NSubstitute;
using Tagger.Application.Playback;
using Tagger.Domain.Tracks;
using Tagger.Desktop.Services;
using Tagger.Desktop.ViewModels;
using Xunit;

namespace Tagger.Desktop.Tests;

public sealed class PlayerViewModelTests
{
    private readonly IPlaybackService _playback = Substitute.For<IPlaybackService>();
    private readonly IUiDispatcher _dispatcher = new ImmediateUiDispatcher();
    private readonly PlayerViewModel _sut;

    public PlayerViewModelTests()
    {
        _playback.Snapshot.Returns(PlaybackSnapshot.Initial);
        _playback.Repeat.Returns(RepeatMode.Off);
        _sut = new PlayerViewModel(_playback, _dispatcher);
        _playback.ClearReceivedCalls();
    }

    private static PlaybackSnapshot Snapshot(
        PlaybackState state = PlaybackState.Playing,
        double positionSeconds = 0,
        double durationSeconds = 180) =>
        new(
            state,
            TimeSpan.FromSeconds(positionSeconds),
            TimeSpan.FromSeconds(durationSeconds),
            0.8d,
            false,
            "music/song.mp3",
            null);

    [Fact]
    public async Task TogglePlayPauseCommand_ShouldCallService_WhenTrackIsLoaded()
    {
        // Arrange
        var track = TestTrack.Create();
        _playback.CurrentTrack.Returns(track);
        _playback.StateChanged += Raise.Event<EventHandler<PlaybackSnapshot>>(_playback, Snapshot());

        // Act
        await _sut.TogglePlayPauseCommand.ExecuteAsync(null);

        // Assert
        await _playback.Received(1).ToggleAsync();
    }

    [Fact]
    public async Task PlayAsync_ShouldForwardQueueAndStartIndex_WhenInvoked()
    {
        // Arrange
        var tracks = new[] { TestTrack.Create("one.mp3"), TestTrack.Create("two.mp3") };

        // Act
        await _sut.PlayAsync(tracks, 1);

        // Assert
        await _playback.Received(1).PlayAsync(
            Arg.Is<IReadOnlyList<Track>>(queue => queue.Count == 2 && queue[1].FilePath == tracks[1].FilePath),
            1,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void StateChanged_ShouldExposeQueueAvailability_WhenPlayerReportsState()
    {
        // Arrange
        _playback.CanGoNext.Returns(true);
        _playback.CanGoPrevious.Returns(false);

        // Act
        _playback.StateChanged += Raise.Event<EventHandler<PlaybackSnapshot>>(_playback, Snapshot());

        // Assert
        Assert.True(_sut.CanGoNext);
        Assert.False(_sut.CanGoPrevious);
        Assert.True(_sut.IsPlaying);
    }

    [Fact]
    public void StateChanged_ShouldFormatHours_WhenDurationCrossesAnHour()
    {
        // Arrange
        _playback.StateChanged += Raise.Event<EventHandler<PlaybackSnapshot>>(
            _playback,
            Snapshot(positionSeconds: 3723, durationSeconds: 7200));

        // Act
        var position = _sut.PositionText;
        var duration = _sut.DurationText;

        // Assert
        Assert.Equal("1:02:03", position);
        Assert.Equal("2:00:00", duration);
    }

    [Fact]
    public void SeekTo_ShouldClampToDuration_WhenValueExceedsTrackLength()
    {
        // Arrange
        _playback.CurrentTrack.Returns(TestTrack.Create());
        _playback.CurrentTrackChanged += Raise.EventWith(_playback, EventArgs.Empty);
        _playback.StateChanged += Raise.Event<EventHandler<PlaybackSnapshot>>(
            _playback,
            Snapshot(durationSeconds: 120));

        // Act
        _sut.SeekTo(500);

        // Assert
        _playback.Received(1).Seek(TimeSpan.FromSeconds(120));
        Assert.Equal(120d, _sut.PositionSeconds);
    }

    [Fact]
    public void BeginScrub_ShouldSuppressPlayerPosition_WhenPlayerKeepsUpdating()
    {
        // Arrange
        _playback.CurrentTrack.Returns(TestTrack.Create());
        _playback.StateChanged += Raise.Event<EventHandler<PlaybackSnapshot>>(
            _playback,
            Snapshot(positionSeconds: 10));

        // Act
        _sut.BeginScrub();
        _playback.StateChanged += Raise.Event<EventHandler<PlaybackSnapshot>>(
            _playback,
            Snapshot(positionSeconds: 25));

        // Assert
        Assert.Equal(10d, _sut.PositionSeconds);
    }

    [Fact]
    public void UpdateScrub_ShouldMovePositionWithoutSeeking_WhenScrubbing()
    {
        // Arrange
        _playback.CurrentTrack.Returns(TestTrack.Create());
        _playback.CurrentTrackChanged += Raise.EventWith(_playback, EventArgs.Empty);
        _playback.StateChanged += Raise.Event<EventHandler<PlaybackSnapshot>>(
            _playback,
            Snapshot(durationSeconds: 120));
        _playback.ClearReceivedCalls();
        _sut.BeginScrub();

        // Act
        _sut.UpdateScrub(75);

        // Assert
        Assert.Equal(75d, _sut.PositionSeconds);
        Assert.Equal("1:15", _sut.PositionText);
        _playback.DidNotReceive().Seek(Arg.Any<TimeSpan>());
    }

    [Fact]
    public void UpdateScrub_ShouldBeIgnored_WhenPointerIsNotDown()
    {
        // Arrange
        _playback.CurrentTrack.Returns(TestTrack.Create());
        _playback.CurrentTrackChanged += Raise.EventWith(_playback, EventArgs.Empty);
        _playback.StateChanged += Raise.Event<EventHandler<PlaybackSnapshot>>(
            _playback,
            Snapshot(positionSeconds: 10, durationSeconds: 120));

        // Act
        _sut.UpdateScrub(75);

        // Assert
        Assert.Equal(10d, _sut.PositionSeconds);
        _playback.DidNotReceive().Seek(Arg.Any<TimeSpan>());
    }

    [Fact]
    public void EndScrub_ShouldSeekToDraggedValue_WhenScrubbingStops()
    {
        // Arrange
        _playback.CurrentTrack.Returns(TestTrack.Create());
        _playback.CurrentTrackChanged += Raise.EventWith(_playback, EventArgs.Empty);
        _playback.StateChanged += Raise.Event<EventHandler<PlaybackSnapshot>>(
            _playback,
            Snapshot(durationSeconds: 120));
        _sut.BeginScrub();

        // Act
        _sut.EndScrub(45);

        // Assert
        _playback.Received(1).Seek(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void Volume_ShouldForwardToServiceAndPersist_WhenSliderChanges()
    {
        // Arrange
        var persisted = false;
        _sut.PreferencesChanged += (_, _) => persisted = true;

        // Act
        _sut.Volume = 55;

        // Assert
        _playback.Received(1).SetVolume(0.55d);
        Assert.True(persisted);
    }

    [Fact]
    public void Failed_ShouldSetErrorMessage_WhenServiceReportsFailure()
    {
        // Arrange
        var surfaced = string.Empty;
        _sut.PlaybackFailed += (_, message) => surfaced = message;

        // Act
        _playback.Failed += Raise.Event<EventHandler<PlaybackFailure>>(
            _playback,
            new PlaybackFailure("music/song.mp3", "unsupported codec"));

        // Assert
        Assert.Equal("unsupported codec", _sut.ErrorMessage);
        Assert.True(_sut.HasError);
        Assert.Equal("unsupported codec", surfaced);
    }

    [Fact]
    public void CurrentTrackChanged_ShouldRefreshMetadataAndArtwork_WhenTrackChanges()
    {
        // Arrange
        var next = TestTrack.Create(
            "next.mp3",
            title: "Next Song",
            artist: "Aurora",
            album: "Night",
            artwork: new CoverArt([1, 2, 3], "image/png"));
        _playback.CurrentTrack.Returns(next);
        _playback.QueueIndex.Returns(3);
        _playback.QueueCount.Returns(12);

        // Act
        _playback.CurrentTrackChanged += Raise.EventWith(_playback, EventArgs.Empty);

        // Assert
        Assert.Equal("Next Song", _sut.Title);
        Assert.Equal("Aurora · Night", _sut.Subtitle);
        Assert.True(_sut.HasArtwork);
        Assert.Equal("4 / 12", _sut.QueueText);
        Assert.Equal(next.FilePath, _sut.CurrentFilePath);
    }

    [Fact]
    public void CycleRepeatCommand_ShouldAdvanceThroughEveryMode_WhenInvoked()
    {
        // Arrange
        _playback.Repeat.Returns(RepeatMode.Off);

        // Act
        _sut.CycleRepeatCommand.Execute(null);
        var afterFirst = _sut.Repeat;
        _sut.CycleRepeatCommand.Execute(null);
        var afterSecond = _sut.Repeat;
        _sut.CycleRepeatCommand.Execute(null);

        // Assert
        Assert.Equal(RepeatMode.All, afterFirst);
        Assert.Equal(RepeatMode.One, afterSecond);
        Assert.Equal(RepeatMode.Off, _sut.Repeat);
        _playback.Received(1).SetRepeat(RepeatMode.All);
        _playback.Received(1).SetRepeat(RepeatMode.One);
    }

    [Fact]
    public void ToggleMuteCommand_ShouldInvertMuteAndNotifyPreferences_WhenInvoked()
    {
        // Arrange
        var persisted = false;
        _sut.PreferencesChanged += (_, _) => persisted = true;

        // Act
        _sut.ToggleMuteCommand.Execute(null);

        // Assert
        Assert.True(_sut.IsMuted);
        _playback.Received(1).SetMuted(true);
        Assert.True(persisted);
    }

    [Fact]
    public void Dispose_ShouldStopPlaybackAndIgnoreLateEvents_WhenWindowCloses()
    {
        // Arrange
        _playback.CurrentTrack.Returns(TestTrack.Create());

        // Act
        _sut.Dispose();
        _playback.StateChanged += Raise.Event<EventHandler<PlaybackSnapshot>>(
            _playback,
            Snapshot(PlaybackState.Failed));

        // Assert
        _playback.Received(1).Stop();
        Assert.False(_sut.HasError);
    }
}
