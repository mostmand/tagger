namespace Tagger.Application.Playback;

public sealed record PlaybackSnapshot(
    PlaybackState State,
    TimeSpan Position,
    TimeSpan Duration,
    double Volume,
    bool IsMuted,
    string? FilePath,
    string? Error)
{
    public static PlaybackSnapshot Initial { get; } = new(
        PlaybackState.Idle,
        TimeSpan.Zero,
        TimeSpan.Zero,
        1d,
        false,
        null,
        null);
}
