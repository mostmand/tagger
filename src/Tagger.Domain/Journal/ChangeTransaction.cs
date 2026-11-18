using Tagger.Domain.Tracks;

namespace Tagger.Domain.Journal;

public sealed record TagWriteChange(
    string FilePath,
    TrackTags Before,
    TrackTags After,
    CoverArt? BeforeArtwork = null,
    CoverArt? AfterArtwork = null);

public sealed record FileMoveChange(string FromPath, string ToPath);

/// <summary>One undoable operation. A batch edit or rename produces exactly one transaction.</summary>
public sealed record ChangeTransaction(
    Guid Id,
    DateTimeOffset Timestamp,
    string Summary,
    bool IsUndone,
    IReadOnlyList<TagWriteChange> TagWrites,
    IReadOnlyList<FileMoveChange> FileMoves)
{
    public bool HasOperations => TagWrites.Count > 0 || FileMoves.Count > 0;

    public bool CanUndo => !IsUndone && HasOperations;

    public ChangeTransaction MarkUndone() => this with { IsUndone = true };

    public static ChangeTransaction Create(
        DateTimeOffset timestamp,
        string summary,
        IReadOnlyList<TagWriteChange>? tagWrites = null,
        IReadOnlyList<FileMoveChange>? fileMoves = null) =>
        new(Guid.NewGuid(), timestamp, summary, false, tagWrites ?? [], fileMoves ?? []);
}
