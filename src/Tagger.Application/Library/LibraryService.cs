using Tagger.Application.Abstractions;
using Tagger.Domain.Tracks;

namespace Tagger.Application.Library;

public interface ILibraryService
{
    Task<LibraryLoadResult> LoadAsync(
        IReadOnlyList<string> paths,
        bool recursive,
        IProgress<LibraryProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Scans paths and loads every readable audio file into a library snapshot.</summary>
public sealed class LibraryService : ILibraryService
{
    private readonly IFileScanner _scanner;
    private readonly ITrackReader _reader;

    public LibraryService(IFileScanner scanner, ITrackReader reader)
    {
        _scanner = scanner;
        _reader = reader;
    }

    public async Task<LibraryLoadResult> LoadAsync(
        IReadOnlyList<string> paths,
        bool recursive,
        IProgress<LibraryProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (paths.Count == 0)
        {
            return new LibraryLoadResult([], [], 0);
        }

        var files = await _scanner.FindAudioFilesAsync(paths, recursive, cancellationToken);
        var tracks = new List<Track>();
        var failures = new List<TrackOperationFailure>();
        var loaded = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new LibraryProgress(loaded, files.Count, file));

            try
            {
                var track = await _reader.ReadAsync(file, cancellationToken);
                tracks.Add(track);
                loaded++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new TrackOperationFailure(file, ex.Message));
            }
        }

        return new LibraryLoadResult(tracks, failures, files.Count);
    }
}
