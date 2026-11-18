using Tagger.Domain.Tracks;

namespace Tagger.Domain.Searching;

public enum TrackSortField
{
    FileName,
    Title,
    Artist,
    Album,
    AlbumArtist,
    Year,
    Track,
    Genre,
    Duration,
    Format,
    Bitrate,
    Path
}

public sealed class TrackComparer : IComparer<Track>
{
    private readonly TrackSortField _field;
    private readonly bool _descending;

    public TrackComparer(TrackSortField field, bool descending)
    {
        _field = field;
        _descending = descending;
    }

    public int Compare(Track? x, Track? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var result = _field switch
        {
            TrackSortField.FileName => NaturalStringComparer.Instance.Compare(x.FileName, y.FileName),
            TrackSortField.Title => Text(x.Tags.Get(TagField.Title), y.Tags.Get(TagField.Title)),
            TrackSortField.Artist => Text(x.Tags.Get(TagField.Artist), y.Tags.Get(TagField.Artist)),
            TrackSortField.Album => Text(x.Tags.Get(TagField.Album), y.Tags.Get(TagField.Album)),
            TrackSortField.AlbumArtist => Text(x.Tags.Get(TagField.AlbumArtist), y.Tags.Get(TagField.AlbumArtist)),
            TrackSortField.Year => Number(x.Tags.Get(TagField.Year), y.Tags.Get(TagField.Year)),
            TrackSortField.Track => Number(x.Tags.Get(TagField.Track), y.Tags.Get(TagField.Track)),
            TrackSortField.Genre => Text(x.Tags.Get(TagField.Genre), y.Tags.Get(TagField.Genre)),
            TrackSortField.Duration => x.Audio?.Duration.CompareTo(y.Audio?.Duration ?? TimeSpan.Zero) ?? 0,
            TrackSortField.Format => Text(x.Format, y.Format),
            TrackSortField.Bitrate => (x.Audio?.BitrateKbps ?? 0).CompareTo(y.Audio?.BitrateKbps ?? 0),
            TrackSortField.Path => NaturalStringComparer.Instance.Compare(x.FilePath, y.FilePath),
            _ => 0
        };

        if (result == 0 && _field != TrackSortField.FileName)
        {
            result = NaturalStringComparer.Instance.Compare(x.FileName, y.FileName);
        }

        return _descending ? -result : result;
    }

    private static int Text(string? x, string? y) =>
        string.Compare(x ?? string.Empty, y ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static int Number(string? x, string? y)
    {
        var xNumber = int.TryParse(x, out var a) ? a : int.MinValue;
        var yNumber = int.TryParse(y, out var b) ? b : int.MinValue;
        return xNumber.CompareTo(yNumber);
    }
}

public static class TrackSorter
{
    public static IReadOnlyList<Track> Sort(
        IEnumerable<Track> tracks,
        TrackSortField field,
        bool descending = false) =>
        tracks.OrderBy(x => x, new TrackComparer(field, descending)).ToList();
}
