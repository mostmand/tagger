using NSubstitute;
using Tagger.Application.Abstractions;
using Tagger.Application.Renaming;
using Tagger.Domain.Journal;
using Tagger.Domain.Naming;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Application.Tests;

public sealed class RenameServiceTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IChangeJournal _journal = Substitute.For<IChangeJournal>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly RenameService _sut;
    private readonly DateTimeOffset _now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    public RenameServiceTests()
    {
        _clock.UtcNow.Returns(_now);
        _sut = new RenameService(_fileSystem, _journal, _clock);
    }

    [Fact]
    public void CreatePlan_ShouldRenderPatternAndAppendExtension_WhenPatternHasNoExtension()
    {
        // Arrange
        var track = TestTracks.Create("01 - old.mp3", title: "Northern Lights", artist: "Aurora");
        var pattern = TagPattern.Parse("%artist% - %title%");

        // Act
        var plan = _sut.CreatePlan([track], pattern);

        // Assert
        var entry = plan.Entries[0];
        Assert.True(entry.IsValid);
        Assert.Equal(Path.Combine("music", "Aurora - Northern Lights.mp3"), entry.TargetPath);
        Assert.Equal(RenameIssue.None, entry.Issue);
    }

    [Fact]
    public void CreatePlan_ShouldNotDuplicateExtension_WhenPatternContainsExt()
    {
        // Arrange
        var track = TestTracks.Create("old.mp3", title: "Song", artist: "Aurora");
        var pattern = TagPattern.Parse("%artist% - %title%.%ext%");

        // Act
        var plan = _sut.CreatePlan([track], pattern);

        // Assert
        Assert.Equal(Path.Combine("music", "Aurora - Song.mp3"), plan.Entries[0].TargetPath);
    }

    [Fact]
    public void CreatePlan_ShouldNotDuplicateExtension_WhenPatternContainsExtensionToken()
    {
        // Arrange
        var track = TestTracks.Create("old.mp3", title: "Song");
        var pattern = TagPattern.Parse("%_filename%%_extension%");

        // Act
        var plan = _sut.CreatePlan([track], pattern);

        // Assert
        Assert.Equal(Path.Combine("music", "old.mp3"), plan.Entries[0].TargetPath);
    }

    [Fact]
    public void CreatePlan_ShouldMarkConflict_WhenTwoTracksResolveToSameName()
    {
        // Arrange
        var first = TestTracks.Create("one.mp3", title: "Same", artist: "Aurora");
        var second = TestTracks.Create("two.mp3", title: "Same", artist: "Aurora");
        var pattern = TagPattern.Parse("%artist% - %title%");

        // Act
        var plan = _sut.CreatePlan([first, second], pattern);

        // Assert
        Assert.Equal(2, plan.ErrorCount);
        Assert.All(plan.Entries, entry => Assert.Equal(RenameIssue.Conflict, entry.Issue));
    }

    [Fact]
    public void CreatePlan_ShouldMarkConflict_WhenTargetAlreadyExists()
    {
        // Arrange
        var track = TestTracks.Create("old.mp3", title: "Song", artist: "Aurora");
        var target = Path.Combine("music", "Aurora - Song.mp3");
        _fileSystem.FileExists(target).Returns(true);
        var pattern = TagPattern.Parse("%artist% - %title%");

        // Act
        var plan = _sut.CreatePlan([track], pattern);

        // Assert
        Assert.Equal(RenameIssue.Conflict, plan.Entries[0].Issue);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMoveFilesAndRecordJournal_WhenPlanIsValid()
    {
        // Arrange
        var track = TestTracks.Create("old.mp3", title: "Song", artist: "Aurora");
        var pattern = TagPattern.Parse("%artist% - %title%");
        var plan = _sut.CreatePlan([track], pattern);
        _fileSystem.FileExists(track.FilePath).Returns(true);
        _fileSystem.FileExists(plan.Entries[0].TargetPath).Returns(false);
        _fileSystem.DirectoryExists("music").Returns(true);

        // Act
        var result = await _sut.ExecuteAsync(plan, "Rename files");

        // Assert
        _fileSystem.Received(1).MoveFile(track.FilePath, plan.Entries[0].TargetPath);
        await _journal.Received(1).RecordAsync(
            Arg.Is<ChangeTransaction>(x => x.FileMoves.Count == 1 && !x.IsUndone),
            Arg.Any<CancellationToken>());
        Assert.Single(result.UpdatedTracks);
        Assert.Equal(plan.Entries[0].TargetPath, result.UpdatedTracks[0].FilePath);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportFailure_WhenSourceNoLongerExists()
    {
        // Arrange
        var track = TestTracks.Create("old.mp3", title: "Song", artist: "Aurora");
        var plan = _sut.CreatePlan([track], TagPattern.Parse("%artist% - %title%"));
        _fileSystem.FileExists(track.FilePath).Returns(false);

        // Act
        var result = await _sut.ExecuteAsync(plan, "Rename files");

        // Assert
        Assert.Empty(result.UpdatedTracks);
        Assert.Single(result.Failures);
        _fileSystem.DidNotReceiveWithAnyArgs().MoveFile(default!, default!);
    }
}
