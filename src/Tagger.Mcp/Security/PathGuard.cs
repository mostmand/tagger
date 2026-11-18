namespace Tagger.Mcp.Security;

/// <summary>Only paths below explicitly allowed roots can be reached by MCP tools.</summary>
public sealed class PathGuard
{
    private readonly IReadOnlyList<string> _roots;

    public PathGuard(IEnumerable<string> allowedRoots)
    {
        _roots = allowedRoots
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => Path.GetFullPath(x).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Distinct(Comparer)
            .ToList();
    }

    public IReadOnlyList<string> Roots => _roots;

    public bool IsAllowed(string path)
    {
        if (_roots.Count == 0 || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var full = Path.GetFullPath(path);
        return _roots.Any(root =>
            Comparer.Equals(full, root) ||
            full.StartsWith(root + Path.DirectorySeparatorChar, Comparison));
    }

    public string EnsureAllowed(string path)
    {
        if (!IsAllowed(path))
        {
            throw new UnauthorizedAccessException($"Path '{path}' is outside the allowed MCP roots.");
        }

        return Path.GetFullPath(path);
    }

    private static StringComparer Comparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static StringComparison Comparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
