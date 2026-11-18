namespace Tagger.Domain.Tracks;

public sealed record CoverArt(byte[] Data, string MimeType, string? Description = null)
{
    public int SizeInBytes => Data.Length;
}

/// <summary>Explicit artwork mutation. Absence of an update is different from removing artwork.</summary>
public readonly record struct CoverArtUpdate(bool ShouldUpdate, CoverArt? Artwork)
{
    public static CoverArtUpdate Keep => new(false, null);

    public static CoverArtUpdate Remove => new(true, null);

    public static CoverArtUpdate Set(CoverArt artwork) => new(true, artwork);

    public bool IsRemoval => ShouldUpdate && Artwork is null;
}
