using Tagger.Domain.Tracks;

namespace Tagger.Application.Duplicates;

public enum DuplicateMatchMode
{
    ArtistAndTitle,
    AlbumAndTrack
}

public sealed record DuplicateGroup(string Key, IReadOnlyList<Track> Tracks)
{
    public int Count => Tracks.Count;
}

public interface IDuplicateFinder
{
    IReadOnlyList<DuplicateGroup> Find(IReadOnlyList<Track> tracks, DuplicateMatchMode mode);
}

/// <summary>Finds likely duplicates by normalized tag values. Audio fingerprinting is a later provider.</summary>
public sealed class DuplicateFinder : IDuplicateFinder
{
    public IReadOnlyList<DuplicateGroup> Find(IReadOnlyList<Track> tracks, DuplicateMatchMode mode)
    {
        if (tracks.Count < 2)
        {
            return [];
        }

        return tracks
            .Select(track => (Track: track, Key: BuildKey(track, mode)))
            .Where(x => x.Key is not null)
            .GroupBy(x => x.Key!, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => new DuplicateGroup(x.Key, x.Select(y => y.Track).ToList()))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? BuildKey(Track track, DuplicateMatchMode mode)
    {
        if (mode == DuplicateMatchMode.ArtistAndTitle)
        {
            var artist = track.Tags.Get(TagField.Artist);
            var title = track.Tags.Get(TagField.Title);
            return string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title)
                ? null
                : $"{Normalize(artist)}\u001f{Normalize(title)}";
        }

        var album = track.Tags.Get(TagField.Album);
        var disc = track.Tags.Get(TagField.Disc) ?? "1";
        var number = track.Tags.Get(TagField.Track);
        return string.IsNullOrWhiteSpace(album) || string.IsNullOrWhiteSpace(number)
            ? null
            : $"{Normalize(album)}\u001f{disc}\u001f{number}";
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
}
