namespace Tagger.Application.Playback;

/// <summary>Plays one file at a time. Implementations must never throw from commands.</summary>
public interface IAudioPlayer : IDisposable
{
    event EventHandler<PlaybackSnapshot>? SnapshotChanged;

    event EventHandler<PlaybackFailure>? Failed;

    event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded;

    Task<bool> PlayAsync(PlaybackRequest request, CancellationToken cancellationToken = default);

    void Pause();

    void Resume();

    void Stop();

    void Seek(TimeSpan position);

    void SetVolume(double volume);

    void SetMuted(bool muted);
}
