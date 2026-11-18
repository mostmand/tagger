using NSubstitute;
using Tagger.Application.Abstractions;
using Tagger.Application.Journal;
using Tagger.Domain.Journal;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Application.Tests;

public sealed class UndoServiceTests
{
    private readonly IChangeJournal _journal = Substitute.For<IChangeJournal>();
    private readonly ITrackWriter _writer = Substitute.For<ITrackWriter>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly UndoService _sut;

    public UndoServiceTests()
    {
        _sut = new UndoService(_journal, _writer, _fileSystem);
    }

    [Fact]
    public async Task UndoLastAsync_ShouldReturnNothing_WhenJournalHasNoTransaction()
    {
        // Arrange
        _journal.GetLastUndoableAsync(Arg.Any<CancellationToken>()).Returns((ChangeTransaction?)null);

        // Act
        var result = await _sut.UndoLastAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("nothing", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UndoLastAsync_ShouldWriteBeforeTagsAndMarkUndone_WhenTransactionHasTagWrite()
    {
        // Arrange
        var before = new TrackTags([new(TagField.Title, "Old")]);
        var after = new TrackTags([new(TagField.Title, "New")]);
        var transaction = ChangeTransaction.Create(
            DateTimeOffset.UnixEpoch,
            "Edit",
            [new TagWriteChange("song.mp3", before, after)]);
        _journal.GetLastUndoableAsync(Arg.Any<CancellationToken>()).Returns(transaction);

        // Act
        var result = await _sut.UndoLastAsync();

        // Assert
        await _writer.Received(1).WriteAsync(
            "song.mp3",
            before,
            CoverArtUpdate.Keep,
            Arg.Any<CancellationToken>());
        await _journal.Received(1).MarkUndoneAsync(transaction.Id, Arg.Any<CancellationToken>());
        Assert.True(result.Success);
    }

    [Fact]
    public async Task UndoLastAsync_ShouldMoveFileBack_WhenTransactionHasRename()
    {
        // Arrange
        var transaction = ChangeTransaction.Create(
            DateTimeOffset.UnixEpoch,
            "Rename",
            fileMoves: [new FileMoveChange("old.mp3", "new.mp3")]);
        _journal.GetLastUndoableAsync(Arg.Any<CancellationToken>()).Returns(transaction);
        _fileSystem.FileExists("new.mp3").Returns(true);
        _fileSystem.FileExists("old.mp3").Returns(false);

        // Act
        var result = await _sut.UndoLastAsync();

        // Assert
        _fileSystem.Received(1).MoveFile("new.mp3", "old.mp3");
        await _journal.Received(1).MarkUndoneAsync(transaction.Id, Arg.Any<CancellationToken>());
        Assert.True(result.Success);
    }

    [Fact]
    public async Task UndoLastAsync_ShouldNotMarkUndone_WhenTagWriteFails()
    {
        // Arrange
        var transaction = ChangeTransaction.Create(
            DateTimeOffset.UnixEpoch,
            "Edit",
            [new TagWriteChange("song.mp3", TrackTags.Empty, TrackTags.Empty)]);
        _journal.GetLastUndoableAsync(Arg.Any<CancellationToken>()).Returns(transaction);
        _writer.WriteAsync(Arg.Any<string>(), Arg.Any<TrackTags>(), Arg.Any<CoverArtUpdate>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Locked")));

        // Act
        var result = await _sut.UndoLastAsync();

        // Assert
        Assert.False(result.Success);
        await _journal.DidNotReceiveWithAnyArgs().MarkUndoneAsync(default, default);
    }
}
