using LibVLCSharp.Shared;
using Tagger.Application.Playback;

namespace Tagger.Infrastructure.Playback;

/// <summary>Pure translations between libVLC values and application playback values.</summary>
public static class VlcPlaybackMapper
{
    public static PlaybackState MapState(VLCState state) => state switch
    {
        VLCState.Opening or VLCState.Buffering => PlaybackState.Opening,
        VLCState.Playing => PlaybackState.Playing,
        VLCState.Paused => PlaybackState.Paused,
        VLCState.Stopped => PlaybackState.Stopped,
        VLCState.Ended => PlaybackState.Ended,
        VLCState.Error => PlaybackState.Failed,
        _ => PlaybackState.Idle
    };

    public static double MapVolume(int volume) => Math.Clamp(volume, 0, 100) / 100d;

    public static TimeSpan NormalizeTime(long milliseconds) =>
        milliseconds <= 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(milliseconds);

    public static string MapError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "This file could not be played.";
        }

        var normalized = message.Trim();
        if (normalized.Contains("codec", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("decode", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("format", StringComparison.OrdinalIgnoreCase))
        {
            return $"This file uses an audio format libVLC cannot decode. ({normalized})";
        }

        return normalized;
    }

    public static string MapError(Exception exception) => exception switch
    {
        DllNotFoundException => "libVLC is not available. Reinstall Tagger or rebuild the Linux runtime.",
        TypeInitializationException => "libVLC could not be initialized. Reinstall Tagger or rebuild the Linux runtime.",
        VLCException => MapError(exception.Message),
        _ => MapError(exception.Message)
    };
}
