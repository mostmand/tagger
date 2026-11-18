namespace Tagger.Domain.Tracks;

public enum TagFieldGroup
{
    Basic,
    Release,
    Credits,
    Advanced
}

public sealed record TagFieldInfo(
    TagField Field,
    string DisplayName,
    string Token,
    TagFieldGroup Group,
    bool IsNumeric = false,
    string? Hint = null);

/// <summary>Static metadata about every supported tag field.</summary>
public static class TagFieldCatalog
{
    private static readonly IReadOnlyList<TagFieldInfo> Fields =
    [
        new(TagField.Title, "Title", "title", TagFieldGroup.Basic, Hint: "Track title"),
        new(TagField.Artist, "Artist", "artist", TagFieldGroup.Basic, Hint: "Performing artist"),
        new(TagField.Album, "Album", "album", TagFieldGroup.Release, Hint: "Album title"),
        new(TagField.AlbumArtist, "Album artist", "albumartist", TagFieldGroup.Release, Hint: "Album-level artist"),
        new(TagField.Year, "Year", "year", TagFieldGroup.Release, IsNumeric: true, Hint: "YYYY"),
        new(TagField.Genre, "Genre", "genre", TagFieldGroup.Basic, Hint: "Genre or style"),
        new(TagField.Track, "Track", "track", TagFieldGroup.Release, IsNumeric: true, Hint: "Track number"),
        new(TagField.TrackTotal, "Track total", "tracktotal", TagFieldGroup.Release, IsNumeric: true),
        new(TagField.Disc, "Disc", "disc", TagFieldGroup.Release, IsNumeric: true, Hint: "Disc number"),
        new(TagField.DiscTotal, "Disc total", "disctotal", TagFieldGroup.Release, IsNumeric: true),
        new(TagField.Composer, "Composer", "composer", TagFieldGroup.Credits, Hint: "Composer or writer"),
        new(TagField.Conductor, "Conductor", "conductor", TagFieldGroup.Credits),
        new(TagField.Lyricist, "Lyricist", "lyricist", TagFieldGroup.Credits),
        new(TagField.Publisher, "Publisher", "publisher", TagFieldGroup.Credits),
        new(TagField.Comment, "Comment", "comment", TagFieldGroup.Advanced, Hint: "Notes or comments"),
        new(TagField.Copyright, "Copyright", "copyright", TagFieldGroup.Advanced),
        new(TagField.EncodedBy, "Encoded by", "encodedby", TagFieldGroup.Advanced),
        new(TagField.Bpm, "BPM", "bpm", TagFieldGroup.Advanced, IsNumeric: true, Hint: "Beats per minute"),
        new(TagField.Isrc, "ISRC", "isrc", TagFieldGroup.Advanced),
        new(TagField.Language, "Language", "language", TagFieldGroup.Advanced),
        new(TagField.Mood, "Mood", "mood", TagFieldGroup.Advanced),
        new(TagField.Grouping, "Grouping", "grouping", TagFieldGroup.Advanced),
        new(TagField.Subtitle, "Subtitle", "subtitle", TagFieldGroup.Advanced),
        new(TagField.OriginalArtist, "Original artist", "originalartist", TagFieldGroup.Advanced),
        new(TagField.OriginalAlbum, "Original album", "originalalbum", TagFieldGroup.Advanced),
        new(TagField.OriginalYear, "Original year", "originalyear", TagFieldGroup.Advanced, IsNumeric: true)
    ];

    private static readonly Dictionary<TagField, TagFieldInfo> ByField = Fields.ToDictionary(x => x.Field);
    private static readonly Dictionary<string, TagFieldInfo> ByToken =
        Fields.ToDictionary(x => x.Token, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<TagFieldInfo> All => Fields;

    public static TagFieldInfo Get(TagField field) => ByField[field];

    public static bool TryGetByToken(string token, out TagFieldInfo info) =>
        ByToken.TryGetValue(token.TrimStart('_'), out info!);

    public static bool TryGetField(string token, out TagField field)
    {
        if (TryGetByToken(token, out var info))
        {
            field = info.Field;
            return true;
        }

        field = default;
        return false;
    }

    public static IEnumerable<TagFieldInfo> InGroup(TagFieldGroup group) => Fields.Where(x => x.Group == group);
}
