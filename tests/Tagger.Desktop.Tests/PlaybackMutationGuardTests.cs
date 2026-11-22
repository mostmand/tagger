using NSubstitute;
using Tagger.Application.Playback;
using Tagger.Domain.Tracks;
using Tagger.Desktop.Services;
using Xunit;

namespace Tagger.Desktop.Tests;

public sealed class PlaybackMutationGuardTests
{
    private readonly IPlaybackService _playback = Substitute.For<IPlaybackService>();
    private readonly PlaybackMutationGuard _sut;

    public PlaybackMutationGuardTests()
    {
        _sut = new PlaybackMutationGuard(_playback);
    }

    [Fact]
    public async Task RunAsync_ShouldResumeUpdatedTrack_WhenMutationSucceeds()
    {
        // Arrange
        var track = TestTrack.Create();
        var updated = track with { FilePath = Path.Combine("music", "renamed.mp3") };
        var continuation = new PlaybackContinuation(track.Id, track.FilePath, TimeSpan.FromSeconds(12), true, 0);
        _playback.PrepareForMutation(Arg.Any<IReadOnlyList<string>>()).Returns(continuation);

        // Act
        var result = await _sut.RunAsync(
            [track.FilePath],
            () => Task.FromResult<IReadOnlyList<Track>>([updated]),
            tracks => tracks);

        // Assert
        Assert.Same(updated, result[0]);
        _playback.Received(1).PrepareForMutation(Arg.Is<IReadOnlyList<string>>(x => x[0] == track.FilePath));
        await _playback.Received(1).ResumeAfterMutationAsync(
            continuation,
            Arg.Is<Track>(candidate => candidate.Id == track.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_ShouldResumeWithoutTrack_WhenMutationThrows()
    {
        // Arrange
        var track = TestTrack.Create();
        var continuation = new PlaybackContinuation(track.Id, track.FilePath, TimeSpan.FromSeconds(4), true, 0);
        _playback.PrepareForMutation(Arg.Any<IReadOnlyList<string>>()).Returns(continuation);

        // Act
        var exception = await Assert.ThrowsAsync<IOException>(() => _sut.RunAsync(
            [track.FilePath],
            () => Task.FromException<IReadOnlyList<Track>>(new IOException("locked")),
            tracks => tracks));

        // Assert
        Assert.Equal("locked", exception.Message);
        await _playback.Received(1).ResumeAfterMutationAsync(
            continuation,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_ShouldNotTouchPlayback_WhenNothingIsPlaying()
    {
        // Arrange
        _playback.PrepareForMutation(Arg.Any<IReadOnlyList<string>>()).Returns((PlaybackContinuation?)null);

        // Act
        await _sut.RunAsync(
            ["music/song.mp3"],
            () => Task.FromResult(42),
            _ => Array.Empty<Track>());

        // Assert
        await _playback.DidNotReceive().ResumeAfterMutationAsync(
            Arg.Any<PlaybackContinuation>(),
            Arg.Any<Track?>(),
            Arg.Any<CancellationToken>());
    }
}
