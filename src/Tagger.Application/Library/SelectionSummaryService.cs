using Tagger.Domain.Tracks;

namespace Tagger.Application.Library;

public sealed record TagFieldSummary(TagField Field, string? CommonValue, bool IsMixed, int ValueCount);

public sealed record SelectionSummary(
    IReadOnlyList<TagFieldSummary> Fields,
    int TrackCount,
    CoverArt? CommonCoverArt,
    bool IsCoverArtMixed)
{
    public bool HasMixedValues => Fields.Any(x => x.IsMixed) || IsCoverArtMixed;

    public TagFieldSummary For(TagField field) =>
        Fields.First(x => x.Field == field);

    public static SelectionSummary Empty { get; } = new([], 0, null, false);
}

public interface ISelectionSummaryService
{
    SelectionSummary Summarize(IReadOnlyList<Track> tracks);
}

/// <summary>Computes common values and mixed states for a multi-selection.</summary>
public sealed class SelectionSummaryService : ISelectionSummaryService
{
    public SelectionSummary Summarize(IReadOnlyList<Track> tracks)
    {
        if (tracks.Count == 0)
        {
            return SelectionSummary.Empty;
        }

        var fields = new List<TagFieldSummary>(TagFieldCatalog.All.Count);
        foreach (var info in TagFieldCatalog.All)
        {
            var values = tracks
                .Select(x => x.Tags.Get(info.Field))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var mixed = values.Count > 1;
            fields.Add(new TagFieldSummary(
                info.Field,
                mixed ? null : values.FirstOrDefault(),
                mixed,
                values.Count));
        }

        var (commonArtwork, artworkMixed) = SummarizeArtwork(tracks);
        return new SelectionSummary(fields, tracks.Count, commonArtwork, artworkMixed);
    }

    private static (CoverArt? Common, bool Mixed) SummarizeArtwork(IReadOnlyList<Track> tracks)
    {
        var first = tracks[0].CoverArt;
        if (first is null)
        {
            return (null, tracks.Any(x => x.CoverArt is not null));
        }

        var mixed = tracks.Any(x => x.CoverArt is null || !SameArtwork(first, x.CoverArt));
        return mixed ? (null, true) : (first, false);
    }

    private static bool SameArtwork(CoverArt left, CoverArt right) =>
        left.MimeType == right.MimeType &&
        left.SizeInBytes == right.SizeInBytes &&
        left.Data.AsSpan().SequenceEqual(right.Data);
}
