using NSubstitute;
using Tagger.Application.Abstractions;
using Tagger.Application.Journal;
using Tagger.Domain.Journal;
using Tagger.Domain.Tracks;
using Tagger.Mcp.Security;
using Tagger.Mcp.Tools;
using Xunit;

namespace Tagger.Mcp.Tests;

public sealed class ChangesUndoToolTests
{
    private readonly IUndoService _undo = Substitute.For<IUndoService>();
    private readonly IChangeJournal _journal = Substitute.For<IChangeJournal>();

    [Fact]
    public async Task InvokeAsync_ShouldUndo_WhenAllPathsAreInsideAllowedRoot()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "tagger-mcp-undo");
        var path = Path.Combine(root, "song.mp3");
        var transaction = ChangeTransaction.Create(
            DateTimeOffset.UnixEpoch,
            "Edit",
            [new TagWriteChange(path, TrackTags.Empty, TrackTags.Empty)]);
        _journal.GetLastUndoableAsync(Arg.Any<CancellationToken>()).Returns(transaction);
        _undo.UndoLastAsync(Arg.Any<CancellationToken>())
            .Returns(new UndoResult(true, "Undone", transaction));
        var _sut = new ChangesUndoTool(_undo, _journal, new PathGuard([root]));

        // Act
        await _sut.InvokeAsync(default);

        // Assert
        await _undo.Received(1).UndoLastAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ShouldRejectUndo_WhenTransactionTouchesOutsideRoot()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "tagger-mcp-undo");
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere", "song.mp3");
        var transaction = ChangeTransaction.Create(
            DateTimeOffset.UnixEpoch,
            "Edit",
            [new TagWriteChange(outside, TrackTags.Empty, TrackTags.Empty)]);
        _journal.GetLastUndoableAsync(Arg.Any<CancellationToken>()).Returns(transaction);
        var _sut = new ChangesUndoTool(_undo, _journal, new PathGuard([root]));

        // Act
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.InvokeAsync(default));

        // Assert
        Assert.Contains("allowed", exception.Message);
        await _undo.DidNotReceiveWithAnyArgs().UndoLastAsync(default);
    }
}
