using Tagger.Application.Abstractions;
using Tagger.Application.Library;
using Tagger.Domain.Journal;
using Tagger.Domain.Naming;
using Tagger.Domain.Tracks;

namespace Tagger.Application.Renaming;

public interface IRenameService
{
    RenamePlan CreatePlan(IReadOnlyList<Track> tracks, TagPattern pattern);

    Task<RenameResult> ExecuteAsync(
        RenamePlan plan,
        string summary,
        CancellationToken cancellationToken = default);
}

/// <summary>Plans and executes file renames derived from tag patterns, with collision checks.</summary>
public sealed class RenameService : IRenameService
{
    private readonly IFileSystem _fileSystem;
    private readonly IChangeJournal _journal;
    private readonly IClock _clock;

    public RenameService(IFileSystem fileSystem, IChangeJournal journal, IClock clock)
    {
        _fileSystem = fileSystem;
        _journal = journal;
        _clock = clock;
    }

    public RenamePlan CreatePlan(IReadOnlyList<Track> tracks, TagPattern pattern)
    {
        if (tracks.Count == 0)
        {
            return RenamePlan.Empty;
        }

        var appendExtension = !pattern.Tokens.Any(x =>
            x.TrimStart('_').Equals("ext", StringComparison.OrdinalIgnoreCase) ||
            x.TrimStart('_').Equals("extension", StringComparison.OrdinalIgnoreCase));

        var entries = tracks
            .Select(track => CreateEntry(track, pattern, appendExtension))
            .ToList();

        MarkDuplicateTargets(entries);
        return new RenamePlan(entries);
    }

    public async Task<RenameResult> ExecuteAsync(
        RenamePlan plan,
        string summary,
        CancellationToken cancellationToken = default)
    {
        var updated = new List<Track>();
        var failures = new List<TrackOperationFailure>();
        var moves = new List<FileMoveChange>();

        foreach (var entry in plan.Entries.Where(x => x.IsValid && !x.IsNoOp))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!_fileSystem.FileExists(entry.OriginalPath))
                {
                    failures.Add(new TrackOperationFailure(entry.OriginalPath, "Source file no longer exists."));
                    continue;
                }

                if (_fileSystem.FileExists(entry.TargetPath))
                {
                    failures.Add(new TrackOperationFailure(entry.TargetPath, "Target file already exists."));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.TargetDirectory) &&
                    !_fileSystem.DirectoryExists(entry.TargetDirectory))
                {
                    _fileSystem.CreateDirectory(entry.TargetDirectory);
                }

                _fileSystem.MoveFile(entry.OriginalPath, entry.TargetPath);
                moves.Add(new FileMoveChange(entry.OriginalPath, entry.TargetPath));
                updated.Add(entry.Track.WithFilePath(entry.TargetPath));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new TrackOperationFailure(entry.OriginalPath, ex.Message));
            }
        }

        ChangeTransaction? transaction = null;
        if (moves.Count > 0)
        {
            transaction = ChangeTransaction.Create(_clock.UtcNow, summary, fileMoves: moves);
            try
            {
                await _journal.RecordAsync(transaction, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new TrackOperationFailure("journal", ex.Message));
            }
        }

        return new RenameResult(updated, failures, transaction);
    }

    private RenamePlanEntry CreateEntry(Track track, TagPattern pattern, bool appendExtension)
    {
        if (!pattern.TryRender(track, out var rendered, out var error))
        {
            return RenamePlanEntry.Invalid(track, track.FilePath, RenameIssue.EmptyName, error ?? "Could not render pattern.");
        }

        var relative = FileNameSanitizer.SanitizeRelativePath(rendered);
        if (relative.Length == 0)
        {
            return RenamePlanEntry.Invalid(
                track,
                track.FilePath,
                RenameIssue.EmptyName,
                "Pattern produced an empty filename.");
        }

        if (appendExtension &&
            !relative.EndsWith($".{track.Extension}", StringComparison.OrdinalIgnoreCase))
        {
            relative += $".{track.Extension}";
        }

        var target = Path.Combine(track.DirectoryPath, relative.Replace('/', Path.DirectorySeparatorChar));

        if (PathComparison.Equals(target, track.FilePath))
        {
            return new RenamePlanEntry(track, target);
        }

        if (_fileSystem.FileExists(target))
        {
            return RenamePlanEntry.Invalid(
                track,
                target,
                RenameIssue.Conflict,
                "A file with this name already exists.");
        }

        return new RenamePlanEntry(track, target);
    }

    private static void MarkDuplicateTargets(List<RenamePlanEntry> entries)
    {
        var duplicateGroups = entries
            .Where(x => x.IsValid)
            .GroupBy(x => x.TargetPath, PathComparison.Comparer)
            .Where(x => x.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            foreach (var entry in group)
            {
                var index = entries.IndexOf(entry);
                if (index >= 0)
                {
                    entries[index] = RenamePlanEntry.Invalid(
                        entry.Track,
                        entry.TargetPath,
                        RenameIssue.Conflict,
                        "Multiple tracks resolve to the same filename.");
                }
            }
        }
    }
}
