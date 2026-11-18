using Tagger.Domain.Tracks;

namespace Tagger.Application.Editing;

public interface IChangePreviewService
{
    ChangePreview Create(
        IReadOnlyList<Track> tracks,
        TagChangeSet changes,
        CoverArtUpdate artwork = default);
}

/// <summary>Builds a field-level diff without touching any file.</summary>
public sealed class ChangeSetPreviewService : IChangePreviewService
{
    public ChangePreview Create(
        IReadOnlyList<Track> tracks,
        TagChangeSet changes,
        CoverArtUpdate artwork = default)
    {
        var previews = new List<TrackChangePreview>(tracks.Count);

        foreach (var track in tracks)
        {
            var newTags = track.Tags.WithMany(changes.ToChanges());
            var diffs = new List<TagFieldDiff>();

            foreach (var info in TagFieldCatalog.All)
            {
                if (!changes.Contains(info.Field))
                {
                    continue;
                }

                var before = track.Tags.Get(info.Field);
                var after = newTags.Get(info.Field);
                if (!string.Equals(before, after, StringComparison.Ordinal))
                {
                    diffs.Add(new TagFieldDiff(info.Field, before, after));
                }
            }

            var coverChanged = artwork.ShouldUpdate &&
                !SameArtwork(track.CoverArt, artwork.Artwork);

            previews.Add(new TrackChangePreview(
                track,
                newTags,
                diffs,
                coverChanged,
                diffs.Count > 0 || coverChanged));
        }

        return new ChangePreview(previews);
    }

    private static bool SameArtwork(CoverArt? left, CoverArt? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.MimeType == right.MimeType &&
               left.Data.AsSpan().SequenceEqual(right.Data);
    }
}
