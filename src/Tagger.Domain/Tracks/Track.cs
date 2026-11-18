namespace Tagger.Domain.Tracks;

/// <summary>A single audio file and everything known about it.</summary>
public sealed record Track
{
    public required Guid Id { get; init; }

    public required string FilePath { get; init; }

    public required string FileName { get; init; }

    /// <summary>Lower-case extension without the leading dot.</summary>
    public required string Extension { get; init; }

    public required TrackTags Tags { get; init; }

    public AudioProperties? Audio { get; init; }

    public CoverArt? CoverArt { get; init; }

    public long FileSizeBytes { get; init; }

    public DateTimeOffset LastModified { get; init; }

    public string DirectoryPath =>
        Path.GetDirectoryName(FilePath) ?? string.Empty;

    public string FileNameWithoutExtension =>
        Path.GetFileNameWithoutExtension(FileName);

    public string Format => Extension.ToUpperInvariant();

    public string DisplayTitle => Tags.Get(TagField.Title) ?? FileNameWithoutExtension;

    public string DisplayArtist => Tags.Get(TagField.Artist) ?? "Unknown artist";

    public string DisplayAlbum => Tags.Get(TagField.Album) ?? string.Empty;

    public string DurationText => Audio?.DurationText ?? "--:--";

    public Track WithTags(TrackTags tags) => this with { Tags = tags };

    public Track WithCoverArt(CoverArt? artwork) => this with { CoverArt = artwork };

    public Track WithFilePath(string filePath) => this with
    {
        FilePath = filePath,
        FileName = Path.GetFileName(filePath)
    };
}
