using Tagger.Domain.Journal;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Domain.Tests;

public sealed class ChangeTransactionTests
{
    [Fact]
    public void CanUndo_ShouldBeTrue_WhenTransactionHasOperationsAndIsNotUndone()
    {
        // Arrange
        var _sut = ChangeTransaction.Create(
            DateTimeOffset.UnixEpoch,
            "Edit tags",
            [new TagWriteChange("song.mp3", TrackTags.Empty, new TrackTags([new(TagField.Title, "Song")]))]);

        // Act
        var canUndo = _sut.CanUndo;

        // Assert
        Assert.True(canUndo);
    }

    [Fact]
    public void CanUndo_ShouldBeFalse_WhenTransactionWasUndone()
    {
        // Arrange
        var transaction = ChangeTransaction.Create(
            DateTimeOffset.UnixEpoch,
            "Edit tags",
            [new TagWriteChange("song.mp3", TrackTags.Empty, new TrackTags([new(TagField.Title, "Song")]))]);
        var _sut = transaction.MarkUndone();

        // Act
        var canUndo = _sut.CanUndo;

        // Assert
        Assert.False(canUndo);
        Assert.True(_sut.IsUndone);
    }

    [Fact]
    public void CanUndo_ShouldBeFalse_WhenTransactionHasNoOperations()
    {
        // Arrange
        var _sut = ChangeTransaction.Create(DateTimeOffset.UnixEpoch, "Empty");

        // Act
        var canUndo = _sut.CanUndo;

        // Assert
        Assert.False(canUndo);
    }
}
