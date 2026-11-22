using NSubstitute;
using Tagger.Application.Renaming;
using Tagger.Desktop.ViewModels;
using Tagger.Domain.Naming;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Desktop.Tests;

public sealed class RenameViewModelTests
{
    private readonly IRenameService _renameService = Substitute.For<IRenameService>();
    private readonly RenameViewModel _sut;

    public RenameViewModelTests()
    {
        _sut = new RenameViewModel(_renameService, new PassThroughMutationGuard());
    }

    [Fact]
    public void Initialize_ShouldPopulateRows_WhenPatternRenders()
    {
        // Arrange
        var track = TestTrack.Create("old.mp3", title: "Song");
        var entry = new RenamePlanEntry(track, Path.Combine("music", "Artist - Song.mp3"));
        _renameService.CreatePlan(Arg.Any<IReadOnlyList<Track>>(), Arg.Any<TagPattern>())
            .Returns(new RenamePlan([entry]));

        // Act
        _sut.Initialize([track], "%artist% - %title%");

        // Assert
        Assert.Single(_sut.Rows);
        Assert.Equal(1, _sut.ChangeCount);
        Assert.Null(_sut.ErrorMessage);
        Assert.True(_sut.CanApply);
    }

    [Fact]
    public void Pattern_ShouldExposeError_WhenPatternIsInvalid()
    {
        // Arrange
        _renameService.CreatePlan(Arg.Any<IReadOnlyList<Track>>(), Arg.Any<TagPattern>())
            .Returns(RenamePlan.Empty);
        _sut.Initialize([TestTrack.Create()], "%artist% - %title%");

        // Act
        _sut.Pattern = "%unknown%";

        // Assert
        Assert.NotNull(_sut.ErrorMessage);
        Assert.Empty(_sut.Rows);
        Assert.False(_sut.CanApply);
    }

    [Fact]
    public async Task ApplyAsync_ShouldExposeUpdatedTracks_WhenServiceSucceeds()
    {
        // Arrange
        var track = TestTrack.Create("old.mp3", title: "Song");
        var target = Path.Combine("music", "Artist - Song.mp3");
        var entry = new RenamePlanEntry(track, target);
        var plan = new RenamePlan([entry]);
        _renameService.CreatePlan(Arg.Any<IReadOnlyList<Track>>(), Arg.Any<TagPattern>()).Returns(plan);
        _renameService.ExecuteAsync(plan, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RenameResult([track.WithFilePath(target)], [], null));
        _sut.Initialize([track], "%artist% - %title%");

        // Act
        var applied = await _sut.ApplyAsync();

        // Assert
        Assert.True(applied);
        Assert.Single(_sut.UpdatedTracks);
        Assert.Equal(target, _sut.UpdatedTracks[0].FilePath);
    }

    [Fact]
    public async Task ApplyAsync_ShouldNotCallService_WhenNothingWouldChange()
    {
        // Arrange
        var track = TestTrack.Create();
        _renameService.CreatePlan(Arg.Any<IReadOnlyList<Track>>(), Arg.Any<TagPattern>())
            .Returns(new RenamePlan([new RenamePlanEntry(track, track.FilePath)]));
        _sut.Initialize([track], "%artist% - %title%");

        // Act
        var applied = await _sut.ApplyAsync();

        // Assert
        Assert.False(applied);
        await _renameService.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!, default);
    }
}
