using Tagger.Domain.Tracks;

namespace Tagger.Application.Abstractions;

public interface ITrackReader
{
    Task<Track> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}

public interface ITrackWriter
{
    Task WriteAsync(
        string filePath,
        TrackTags tags,
        CoverArtUpdate artwork,
        CancellationToken cancellationToken = default);
}
