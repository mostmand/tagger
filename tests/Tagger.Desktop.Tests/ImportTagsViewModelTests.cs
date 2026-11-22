using NSubstitute;
using Tagger.Application.Editing;
using Tagger.Application.Importing;
using Tagger.Desktop.ViewModels;
using Tagger.Domain.Naming;
using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Desktop.Tests;

public sealed class ImportTagsViewModelTests
{
    private readonly IFilenameImportService _importService = Substitute.For<IFilenameImportService>();
    private readonly ITagSaveService _saveService = Substitute.For<ITagSaveService>();
    private readonly ImportTagsViewModel _sut;

    public ImportTagsViewModelTests()
    {
        _sut = new ImportTagsViewModel(_importService, _saveService, new PassThroughMutationGuard());
    }

    [Fact]
    public void Initialize_ShouldPopulateRows_WhenPatternIsValid()
    {
        // Arrange
        var track = TestTrack.Create("Artist - Song.mp3", title: "Song");
        var entry = new FilenameImportEntry(
            track,
            new TrackTags([new(TagField.Artist, "Artist")]),
            new TagChangeSet().Add(TagField.Artist, "Artist"));
        _importService.CreatePlan(Arg.Any<IReadOnlyList<Track>>(), Arg.Any<TagPattern>())
            .Returns(new FilenameImportPlan([entry]));

        // Act
        _sut.Initialize([track], "%artist% - %title%");

        // Assert
        Assert.Single(_sut.Rows);
        Assert.Equal(1, _sut.ChangeCount);
        Assert.True(_sut.CanApply);
    }

    [Fact]
    public void Pattern_ShouldExposeError_WhenPatternIsInvalid()
    {
        // Arrange
        _importService.CreatePlan(Arg.Any<IReadOnlyList<Track>>(), Arg.Any<TagPattern>())
            .Returns(new FilenameImportPlan([]));
        _sut.Initialize([TestTrack.Create()], "%artist% - %title%");

        // Act
        _sut.Pattern = "%nope%";

        // Assert
        Assert.NotNull(_sut.ErrorMessage);
        Assert.False(_sut.CanApply);
    }

    [Fact]
    public async Task ApplyAsync_ShouldSaveOnlyChangedTracks_WhenInvoked()
    {
        // Arrange
        var changed = TestTrack.Create("one.mp3");
        var unchanged = TestTrack.Create("two.mp3");
        var plan = new FilenameImportPlan(
        [
            new FilenameImportEntry(
                changed,
                new TrackTags([new(TagField.Artist, "Artist")]),
                new TagChangeSet().Add(TagField.Artist, "Artist")),
            new FilenameImportEntry(unchanged, unchanged.Tags, TagChangeSet.Empty)
        ]);
        _importService.CreatePlan(Arg.Any<IReadOnlyList<Track>>(), Arg.Any<TagPattern>()).Returns(plan);
        _saveService.ApplyAsync(Arg.Any<IReadOnlyList<TagEditRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SaveResult([changed], [], null));
        _sut.Initialize([changed, unchanged], "%artist% - %title%");

        // Act
        var applied = await _sut.ApplyAsync();

        // Assert
        Assert.True(applied);
        await _saveService.Received(1).ApplyAsync(
            Arg.Is<IReadOnlyList<TagEditRequest>>(x => x.Count == 1),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        Assert.Single(_sut.UpdatedTracks);
    }
}
