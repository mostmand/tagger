using Tagger.Application.Abstractions;
using Tagger.Application.Library;
using Tagger.Domain.Journal;
using Tagger.Domain.Tracks;

namespace Tagger.Application.Editing;

public interface ITagSaveService
{
    Task<SaveResult> ApplyAsync(
        IReadOnlyList<TagEditRequest> requests,
        string summary,
        CancellationToken cancellationToken = default);

    Task<SaveResult> ApplyAsync(
        IReadOnlyList<Track> tracks,
        TagChangeSet changes,
        CoverArtUpdate artwork,
        string summary,
        CancellationToken cancellationToken = default);
}

/// <summary>Writes tag patches, updating the journal as one undoable transaction.</summary>
public sealed class TagSaveService : ITagSaveService
{
    private readonly ITrackWriter _writer;
    private readonly IChangeJournal _journal;
    private readonly IClock _clock;

    public TagSaveService(ITrackWriter writer, IChangeJournal journal, IClock clock)
    {
        _writer = writer;
        _journal = journal;
        _clock = clock;
    }

    public Task<SaveResult> ApplyAsync(
        IReadOnlyList<Track> tracks,
        TagChangeSet changes,
        CoverArtUpdate artwork,
        string summary,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            tracks.Select(track => new TagEditRequest(track, changes, artwork)).ToList(),
            summary,
            cancellationToken);

    public async Task<SaveResult> ApplyAsync(
        IReadOnlyList<TagEditRequest> requests,
        string summary,
        CancellationToken cancellationToken = default)
    {
        var updated = new List<Track>();
        var failures = new List<TrackOperationFailure>();
        var writes = new List<TagWriteChange>();

        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!request.HasChanges)
            {
                continue;
            }

            var track = request.Track;
            var newTags = track.Tags.WithMany(request.Changes.ToChanges());

            try
            {
                await _writer.WriteAsync(track.FilePath, newTags, request.CoverArt, cancellationToken);

                var newArtwork = request.CoverArt.ShouldUpdate
                    ? request.CoverArt.Artwork
                    : track.CoverArt;

                updated.Add(track.WithTags(newTags).WithCoverArt(newArtwork) with
                {
                    LastModified = _clock.UtcNow
                });

                if (!newTags.Equals(track.Tags) || request.CoverArt.ShouldUpdate)
                {
                    writes.Add(new TagWriteChange(
                        track.FilePath,
                        track.Tags,
                        newTags,
                        track.CoverArt,
                        newArtwork));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new TrackOperationFailure(track.FilePath, ex.Message));
            }
        }

        ChangeTransaction? transaction = null;
        if (writes.Count > 0)
        {
            transaction = ChangeTransaction.Create(_clock.UtcNow, summary, writes);
            try
            {
                await _journal.RecordAsync(transaction, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new TrackOperationFailure("journal", ex.Message));
            }
        }

        return new SaveResult(updated, failures, transaction);
    }
}
