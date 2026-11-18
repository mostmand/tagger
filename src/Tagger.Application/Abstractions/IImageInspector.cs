namespace Tagger.Application.Abstractions;

public sealed record ImageInfo(string MimeType, int Width, int Height);

public interface IImageInspector
{
    ImageInfo? Inspect(byte[] data);
}
