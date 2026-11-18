using Tagger.Application.Renaming;

namespace Tagger.Desktop.ViewModels;

/// <summary>Renders a directory as a short, readable label relative to the loaded library roots.</summary>
public sealed class FolderLabelFormatter
{
    private readonly IReadOnlyList<string> _roots;

    public FolderLabelFormatter(IReadOnlyList<string> roots)
    {
        _roots = roots
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize)
            .Distinct(PathComparison.Comparer)
            .ToList();
    }

    public string Format(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return string.Empty;
        }

        var directory = Normalize(directoryPath);

        foreach (var root in _roots)
        {
            if (PathComparison.Comparer.Equals(directory, root))
            {
                return FolderName(root);
            }

            if (IsUnder(directory, root))
            {
                var relative = directory[root.Length..]
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                return _roots.Count > 1
                    ? Path.Combine(FolderName(root), relative)
                    : relative;
            }
        }

        return directory;
    }

    private static bool IsUnder(string directory, string root)
    {
        if (root.EndsWith(Path.DirectorySeparatorChar))
        {
            return directory.StartsWith(root, PathComparison.Comparison);
        }

        return directory.StartsWith(root + Path.DirectorySeparatorChar, PathComparison.Comparison);
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(path);

    private static string FolderName(string path)
    {
        var name = Path.GetFileName(path);
        return string.IsNullOrEmpty(name) ? path : name;
    }
}
