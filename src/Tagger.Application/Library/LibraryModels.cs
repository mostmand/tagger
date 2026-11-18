namespace Tagger.Application.Library;

using Tagger.Domain.Tracks;

public sealed record TrackOperationFailure(string Path, string Message);

public sealed record LibraryProgress(int Loaded, int Total, string CurrentFile);

public sealed record LibraryLoadResult(
    IReadOnlyList<Track> Tracks,
    IReadOnlyList<TrackOperationFailure> Failures,
    int ScannedFileCount);
