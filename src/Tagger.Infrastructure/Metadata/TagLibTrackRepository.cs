using Tagger.Application.Abstractions;
using Tagger.Domain.Tracks;

namespace Tagger.Infrastructure.Metadata;

/// <summary>
/// TagLib# adapter with an atomic replace: tags are written to a sibling copy,
/// then the copy replaces the original only after a successful save.
/// </summary>
public sealed class TagLibTrackRepository : ITrackReader, ITrackWriter
{
    public Task<Track> ReadAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.Run(() => TagLibTagMapper.ReadTrack(filePath, Guid.NewGuid()), cancellationToken);

    public Task WriteAsync(
        string filePath,
        TrackTags tags,
        CoverArtUpdate artwork,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => WriteCore(filePath, tags, artwork), cancellationToken);

    private static void WriteCore(string filePath, TrackTags tags, CoverArtUpdate artwork)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException($"File '{filePath}' has no directory.");
        var extension = Path.GetExtension(filePath);
        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileNameWithoutExtension(filePath)}.tagger-{Guid.NewGuid():N}{extension}");

        UnixFileMode? mode = null;
        DateTime? lastWrite = null;

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                mode = File.GetUnixFileMode(filePath);
            }

            lastWrite = File.GetLastWriteTimeUtc(filePath);
            File.Copy(filePath, tempPath, overwrite: true);

            using (var file = TagLib.File.Create(tempPath))
            {
                TagLibTagMapper.ApplyTags(file, tags);
                TagLibTagMapper.ApplyArtwork(file.Tag, artwork);
                file.Save();
            }

            File.Move(tempPath, filePath, overwrite: true);

            if (!OperatingSystem.IsWindows() && mode is not null)
            {
                File.SetUnixFileMode(filePath, mode.Value);
            }

            if (lastWrite is not null)
            {
                File.SetLastWriteTimeUtc(filePath, lastWrite.Value);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                    // Best-effort cleanup; the original file is unaffected.
                }
            }
        }
    }
}
