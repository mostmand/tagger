using Tagger.Application.Playback;
using Tagger.Domain.Tracks;

namespace Tagger.Desktop.Services;

/// <summary>
/// Stops playback around a file mutation and resumes the mutated track afterwards.
/// TagLib replaces files in place, so a player holding the old handle can block the
/// write on Windows and reads stale data elsewhere.
/// </summary>
public interface IPlaybackMutationGuard
{
    Task<T> RunAsync<T>(
        IReadOnlyList<string> paths,
        Func<Task<T>> mutation,
        Func<T, IReadOnlyList<Track>> updatedTracks,
        CancellationToken cancellationToken = default);
}

public sealed class PlaybackMutationGuard : IPlaybackMutationGuard
{
    private readonly IPlaybackService _playback;

    public PlaybackMutationGuard(IPlaybackService playback)
    {
        _playback = playback;
    }

    public async Task<T> RunAsync<T>(
        IReadOnlyList<string> paths,
        Func<Task<T>> mutation,
        Func<T, IReadOnlyList<Track>> updatedTracks,
        CancellationToken cancellationToken = default)
    {
        var continuation = _playback.PrepareForMutation(paths);
        try
        {
            var result = await mutation().ConfigureAwait(false);
            await ResumeAsync(continuation, updatedTracks(result)).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await ResumeAsync(continuation, []).ConfigureAwait(false);
            throw;
        }
    }

    private Task ResumeAsync(PlaybackContinuation? continuation, IReadOnlyList<Track> updated)
    {
        if (continuation is null)
        {
            return Task.CompletedTask;
        }

        var track = updated.FirstOrDefault(candidate => candidate.Id == continuation.TrackId);
        return _playback.ResumeAfterMutationAsync(continuation, track, CancellationToken.None);
    }
}
