using Tagger.Domain.Tracks;

namespace Tagger.Application.Playback;

public interface IPlaybackService : IDisposable
{
    PlaybackSnapshot Snapshot { get; }

    Track? CurrentTrack { get; }

    RepeatMode Repeat { get; }

    bool CanGoNext { get; }

    bool CanGoPrevious { get; }

    int QueueIndex { get; }

    int QueueCount { get; }

    event EventHandler<PlaybackSnapshot>? StateChanged;

    event EventHandler? CurrentTrackChanged;

    event EventHandler<PlaybackFailure>? Failed;

    Task PlayAsync(
        IReadOnlyList<Track> queue,
        int startIndex,
        CancellationToken cancellationToken = default);

    Task ToggleAsync(CancellationToken cancellationToken = default);

    Task NextAsync(CancellationToken cancellationToken = default);

    Task PreviousAsync(CancellationToken cancellationToken = default);

    void Stop();

    void Seek(TimeSpan position);

    void SetVolume(double volume);

    void SetMuted(bool muted);

    void SetRepeat(RepeatMode repeat);

    void RemoveFromQueue(IReadOnlyList<string> paths);

    void UpdateTrack(Track track, string? previousPath = null);

    /// <summary>Stops playback before a mutation and returns enough state to resume it.</summary>
    PlaybackContinuation? PrepareForMutation(IReadOnlyList<string> paths);

    Task ResumeAfterMutationAsync(
        PlaybackContinuation continuation,
        Track? updatedTrack = null,
        CancellationToken cancellationToken = default);
}
