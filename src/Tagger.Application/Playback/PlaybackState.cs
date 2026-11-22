namespace Tagger.Application.Playback;

public enum PlaybackState
{
    Idle,
    Opening,
    Playing,
    Paused,
    Stopped,
    Ended,
    Failed
}

public enum RepeatMode
{
    Off,
    All,
    One
}

/// <summary>State captured before a file mutation so playback can continue afterwards.</summary>
public sealed record PlaybackContinuation(
    Guid TrackId,
    string FilePath,
    TimeSpan Position,
    bool WasPlaying,
    int QueueIndex);
