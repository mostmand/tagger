using Tagger.Application.Playback;
using Tagger.Domain.Tracks;
using Tagger.Desktop.Services;

namespace Tagger.Desktop.Tests;

internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

internal sealed class PassThroughMutationGuard : IPlaybackMutationGuard
{
    public Task<T> RunAsync<T>(
        IReadOnlyList<string> paths,
        Func<Task<T>> mutation,
        Func<T, IReadOnlyList<Track>> updatedTracks,
        CancellationToken cancellationToken = default) =>
        mutation();
}
