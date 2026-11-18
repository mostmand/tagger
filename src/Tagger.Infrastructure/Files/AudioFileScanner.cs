using Tagger.Application.Abstractions;

namespace Tagger.Infrastructure.Files;

public sealed class AudioFileScanner : IFileScanner
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".ogg", ".opus", ".spx",
        ".m4a", ".mp4", ".m4b", ".aac",
        ".wav", ".aiff", ".aif",
        ".ape", ".wv", ".mpc", ".wma",
        ".dsf", ".dff"
    };

    public Task<IReadOnlyList<string>> FindAudioFilesAsync(
        IEnumerable<string> paths,
        bool recursive,
        CancellationToken cancellationToken = default)
    {
        var seen = new HashSet<string>(PathComparer());
        var files = new List<string>();

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                if (IsSupported(path))
                {
                    Add(path, seen, files);
                }

                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var file in Directory.EnumerateFiles(path, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsSupported(file))
                {
                    Add(file, seen, files);
                }
            }
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    private static void Add(string path, HashSet<string> seen, List<string> files)
    {
        var full = Path.GetFullPath(path);
        if (seen.Add(full))
        {
            files.Add(full);
        }
    }

    private static StringComparer PathComparer() =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}
