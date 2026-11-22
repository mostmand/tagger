namespace Tagger.Application.Playback;

public sealed record PlaybackFailure(string FilePath, string Message);

/// <summary>Identifies the media that reached its end, so stale events can be ignored.</summary>
public sealed record PlaybackEndedEventArgs(string FilePath);

public sealed record PlaybackRequest(
    string FilePath,
    TimeSpan StartPosition,
    string? DisplayTitle);
