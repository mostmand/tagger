using Tagger.Application.Abstractions;
using Tagger.Application.Library;
using Tagger.Application.Renaming;
using Tagger.Domain.Journal;
using Tagger.Domain.Tracks;

namespace Tagger.Application.Journal;

public sealed record UndoResult(bool Success, string Message, ChangeTransaction? Transaction)
{
    public static UndoResult Nothing { get; } = new(false, "There is nothing to undo.", null);
}

public interface IUndoService
{
    Task<UndoResult> UndoLastAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reverses the most recent journaled tag writes and file moves.</summary>
public sealed class UndoService : IUndoService
{
    private readonly IChangeJournal _journal;
    private readonly ITrackWriter _writer;
    private readonly IFileSystem _fileSystem;

    public UndoService(IChangeJournal journal, ITrackWriter writer, IFileSystem fileSystem)
    {
        _journal = journal;
        _writer = writer;
        _fileSystem = fileSystem;
    }

    public async Task<UndoResult> UndoLastAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _journal.GetLastUndoableAsync(cancellationToken);
        if (transaction is null || !transaction.CanUndo)
        {
            return UndoResult.Nothing;
        }

        var failures = new List<TrackOperationFailure>();
        var pathMap = new Dictionary<string, string>(PathComparison.Comparer);

        for (var i = transaction.FileMoves.Count - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var move = transaction.FileMoves[i];

            try
            {
                if (!_fileSystem.FileExists(move.ToPath))
                {
                    failures.Add(new TrackOperationFailure(move.ToPath, "Renamed file no longer exists."));
                    continue;
                }

                if (_fileSystem.FileExists(move.FromPath))
                {
                    failures.Add(new TrackOperationFailure(move.FromPath, "Original path is occupied."));
                    continue;
                }

                _fileSystem.MoveFile(move.ToPath, move.FromPath);
                pathMap[move.ToPath] = move.FromPath;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new TrackOperationFailure(move.ToPath, ex.Message));
            }
        }

        for (var i = transaction.TagWrites.Count - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var write = transaction.TagWrites[i];
            var path = pathMap.GetValueOrDefault(write.FilePath, write.FilePath);
            var artwork = ToUpdate(write.BeforeArtwork, write.AfterArtwork);

            try
            {
                await _writer.WriteAsync(path, write.Before, artwork, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new TrackOperationFailure(path, ex.Message));
            }
        }

        if (failures.Count > 0)
        {
            return new UndoResult(false, $"Undo incomplete: {failures[0].Message}", transaction);
        }

        await _journal.MarkUndoneAsync(transaction.Id, cancellationToken);
        return new UndoResult(true, "Last change was undone.", transaction.MarkUndone());
    }

    private static CoverArtUpdate ToUpdate(CoverArt? before, CoverArt? after)
    {
        if (before is null && after is null)
        {
            return CoverArtUpdate.Keep;
        }

        if (before is not null && after is not null &&
            before.MimeType == after.MimeType &&
            before.Data.AsSpan().SequenceEqual(after.Data))
        {
            return CoverArtUpdate.Keep;
        }

        return before is null ? CoverArtUpdate.Remove : CoverArtUpdate.Set(before);
    }
}
