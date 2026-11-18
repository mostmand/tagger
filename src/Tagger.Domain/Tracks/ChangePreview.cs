namespace Tagger.Domain.Tracks;

public sealed record TagFieldDiff(TagField Field, string? Before, string? After)
{
    public bool IsChanged => !string.Equals(Before, After, StringComparison.Ordinal);
}

public sealed record TrackChangePreview(
    Track Track,
    TrackTags NewTags,
    IReadOnlyList<TagFieldDiff> Diffs,
    bool CoverArtChanged,
    bool HasChanges)
{
    public string Summary
    {
        get
        {
            if (!HasChanges)
            {
                return "No changes";
            }

            var parts = new List<string>();
            if (Diffs.Count > 0)
            {
                parts.Add($"{Diffs.Count} field{(Diffs.Count == 1 ? string.Empty : "s")}");
            }

            if (CoverArtChanged)
            {
                parts.Add("artwork");
            }

            return string.Join(", ", parts);
        }
    }
}

public sealed record ChangePreview(IReadOnlyList<TrackChangePreview> Tracks)
{
    public int ChangedTrackCount => Tracks.Count(x => x.HasChanges);

    public int ChangedFieldCount => Tracks.Sum(x => x.Diffs.Count);

    public bool HasChanges => Tracks.Any(x => x.HasChanges);

    public static ChangePreview Empty { get; } = new([]);
}
