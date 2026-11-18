using Tagger.Domain.Journal;
using Tagger.Domain.Tracks;
using Tagger.Infrastructure.Journal;
using Xunit;

namespace Tagger.Infrastructure.Tests;

public sealed class JsonChangeJournalTests
{
    [Fact]
    public async Task RecordAsync_ShouldReturnTransactionAsLastUndoable_WhenWritten()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "tagger-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "journal.jsonl");
        var _sut = new JsonChangeJournal(path);
        var transaction = ChangeTransaction.Create(
            DateTimeOffset.UnixEpoch,
            "Edit",
            [new TagWriteChange("song.mp3", TrackTags.Empty, new TrackTags([new(TagField.Title, "Song")]))]);

        try
        {
            // Act
            await _sut.RecordAsync(transaction);
            var loaded = await _sut.GetLastUndoableAsync();

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(transaction.Id, loaded.Id);
            Assert.Equal("Song", loaded.TagWrites[0].After.Get(TagField.Title));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MarkUndoneAsync_ShouldMakeTransactionUnavailable_WhenCalled()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "tagger-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "journal.jsonl");
        var _sut = new JsonChangeJournal(path);
        var transaction = ChangeTransaction.Create(
            DateTimeOffset.UnixEpoch,
            "Rename",
            fileMoves: [new FileMoveChange("old.mp3", "new.mp3")]);

        try
        {
            await _sut.RecordAsync(transaction);

            // Act
            await _sut.MarkUndoneAsync(transaction.Id);
            var loaded = await _sut.GetLastUndoableAsync();

            // Assert
            Assert.Null(loaded);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
