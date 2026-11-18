using Tagger.Domain.Tracks;

namespace Tagger.Application.Renaming;

public enum RenameIssue
{
    None,
    EmptyName,
    Conflict,
    MissingFile,
    Failed
}

public sealed record RenamePlanEntry(
    Track Track,
    string TargetPath,
    RenameIssue Issue = RenameIssue.None,
    string? Error = null)
{
    public bool IsValid => Issue == RenameIssue.None;

    public bool IsNoOp => IsValid && PathComparison.Equals(Track.FilePath, TargetPath);

    public string OriginalPath => Track.FilePath;

    public string TargetFileName => Path.GetFileName(TargetPath);

    public string TargetDirectory => Path.GetDirectoryName(TargetPath) ?? string.Empty;

    public static RenamePlanEntry Invalid(Track track, string target, RenameIssue issue, string error) =>
        new(track, target, issue, error);
}

public sealed record RenamePlan(IReadOnlyList<RenamePlanEntry> Entries)
{
    public int ErrorCount => Entries.Count(x => !x.IsValid);

    public int ChangeCount => Entries.Count(x => x.IsValid && !x.IsNoOp);

    public bool HasErrors => ErrorCount > 0;

    public bool HasChanges => ChangeCount > 0;

    public static RenamePlan Empty { get; } = new([]);
}

public sealed record RenameResult(
    IReadOnlyList<Track> UpdatedTracks,
    IReadOnlyList<Library.TrackOperationFailure> Failures,
    Domain.Journal.ChangeTransaction? Transaction)
{
    public bool IsSuccess => Failures.Count == 0;
}

public static class PathComparison
{
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    public static StringComparer Comparer { get; } = StringComparer.FromComparison(Comparison);

    public static bool Equals(string left, string right) => Comparer.Equals(left, right);
}
