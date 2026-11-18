using Tagger.Domain.Tracks;

namespace Tagger.Application.Editing;

public sealed record TagEditRequest(
    Track Track,
    TagChangeSet Changes,
    CoverArtUpdate CoverArt = default)
{
    public static TagEditRequest For(Track track, TagChangeSet changes) => new(track, changes);

    public bool HasChanges => !Changes.IsEmpty || CoverArt.ShouldUpdate;
}

public sealed record SaveResult(
    IReadOnlyList<Track> UpdatedTracks,
    IReadOnlyList<Library.TrackOperationFailure> Failures,
    Domain.Journal.ChangeTransaction? Transaction)
{
    public bool IsSuccess => Failures.Count == 0;

    public static SaveResult Empty { get; } = new([], [], null);
}
