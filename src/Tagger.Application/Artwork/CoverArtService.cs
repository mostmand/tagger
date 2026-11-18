using Tagger.Application.Abstractions;
using Tagger.Domain.Tracks;

namespace Tagger.Application.Artwork;

public sealed record CoverArtResult(bool Success, CoverArt? Artwork, string? Error)
{
    public static CoverArtResult Ok(CoverArt artwork) => new(true, artwork, null);

    public static CoverArtResult Fail(string error) => new(false, null, error);
}

public interface ICoverArtService
{
    CoverArtResult Create(byte[] data, string? description = null);
}

/// <summary>Validates image bytes before they can be embedded in a file.</summary>
public sealed class CoverArtService : ICoverArtService
{
    public const int MaxSizeBytes = 20 * 1024 * 1024;

    private readonly IImageInspector _inspector;

    public CoverArtService(IImageInspector inspector)
    {
        _inspector = inspector;
    }

    public CoverArtResult Create(byte[] data, string? description = null)
    {
        if (data.Length == 0)
        {
            return CoverArtResult.Fail("The image file is empty.");
        }

        if (data.Length > MaxSizeBytes)
        {
            return CoverArtResult.Fail("Artwork must be 20 MB or smaller.");
        }

        var info = _inspector.Inspect(data);
        if (info is null)
        {
            return CoverArtResult.Fail("Unsupported image format. Use PNG, JPEG, GIF, BMP, or WebP.");
        }

        if (info.Width <= 0 || info.Height <= 0)
        {
            return CoverArtResult.Fail("The image has invalid dimensions.");
        }

        return CoverArtResult.Ok(new CoverArt(data, info.MimeType, description?.Trim()));
    }
}
