using NSubstitute;
using Tagger.Application.Abstractions;
using Tagger.Application.Artwork;
using Tagger.Application.Editing;
using Tagger.Application.Library;
using Tagger.Domain.Tracks;
using Tagger.Desktop.Services;
using Tagger.Desktop.ViewModels;
using Xunit;

namespace Tagger.Desktop.Tests;

public sealed class TagEditorViewModelTests
{
    private readonly ITagSaveService _saveService = Substitute.For<ITagSaveService>();
    private readonly IChangePreviewService _previewService = Substitute.For<IChangePreviewService>();
    private readonly ICoverArtService _coverArt = Substitute.For<ICoverArtService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly TagEditorViewModel _sut;

    public TagEditorViewModelTests()
    {
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero));
        _sut = new TagEditorViewModel(
            new SelectionSummaryService(),
            _saveService,
            _previewService,
            _coverArt,
            _dialogs,
            _clock);
    }

    [Fact]
    public void LoadSelection_ShouldShowMixedValue_WhenTracksDiffer()
    {
        // Arrange
        var tracks = new[]
        {
            TestTrack.Create("one.mp3", title: "Alpha"),
            TestTrack.Create("two.mp3", title: "Bravo")
        };

        // Act
        _sut.LoadSelection(tracks);

        // Assert
        var title = _sut.AllFields.First(x => x.Field == TagField.Title);
        Assert.True(title.IsMixed);
        Assert.Equal(string.Empty, title.Value);
        Assert.Equal("Mixed values", title.PlaceholderText);
        Assert.True(_sut.HasSelection);
    }

    [Fact]
    public void LoadSelection_ShouldShowCommonValue_WhenTracksMatch()
    {
        // Arrange
        var tracks = new[]
        {
            TestTrack.Create("one.mp3", artist: "Aurora"),
            TestTrack.Create("two.mp3", artist: "Aurora")
        };

        // Act
        _sut.LoadSelection(tracks);

        // Assert
        var artist = _sut.AllFields.First(x => x.Field == TagField.Artist);
        Assert.False(artist.IsMixed);
        Assert.Equal("Aurora", artist.Value);
    }

    [Fact]
    public async Task SaveAsync_ShouldDoNothing_WhenNoFieldChanged()
    {
        // Arrange
        _sut.LoadSelection([TestTrack.Create()]);

        // Act
        await _sut.SaveAsync(previewRequired: false);

        // Assert
        await _saveService.DidNotReceiveWithAnyArgs().ApplyAsync(
            default!, default!, default);
        Assert.Contains("No changes", _sut.StatusMessage);
    }

    [Fact]
    public async Task SaveAsync_ShouldNotWrite_WhenPreviewIsCancelled()
    {
        // Arrange
        var track = TestTrack.Create(title: "Old");
        _sut.LoadSelection([track]);
        _sut.AllFields.First(x => x.Field == TagField.Title).Value = "New";
        _previewService.Create(Arg.Any<IReadOnlyList<Track>>(), Arg.Any<TagChangeSet>(), Arg.Any<CoverArtUpdate>())
            .Returns(new ChangePreview([new TrackChangePreview(track, track.Tags, [new TagFieldDiff(TagField.Title, "Old", "New")], false, true)]));
        _dialogs.ShowChangePreviewAsync(Arg.Any<ChangePreview>(), Arg.Any<string>()).Returns(false);

        // Act
        await _sut.SaveAsync(previewRequired: true);

        // Assert
        await _saveService.DidNotReceiveWithAnyArgs().ApplyAsync(
            default!, default!, default);
        Assert.Contains("cancelled", _sut.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_ShouldWriteChangesAndRaiseSaved_WhenConfirmed()
    {
        // Arrange
        var track = TestTrack.Create(title: "Old");
        var updated = track.WithTags(track.Tags.With(TagField.Title, "New"));
        _sut.LoadSelection([track]);
        _sut.AllFields.First(x => x.Field == TagField.Title).Value = "New";
        _previewService.Create(Arg.Any<IReadOnlyList<Track>>(), Arg.Any<TagChangeSet>(), Arg.Any<CoverArtUpdate>())
            .Returns(new ChangePreview([new TrackChangePreview(track, updated.Tags, [new TagFieldDiff(TagField.Title, "Old", "New")], false, true)]));
        _dialogs.ShowChangePreviewAsync(Arg.Any<ChangePreview>(), Arg.Any<string>()).Returns(true);
        _saveService.ApplyAsync(Arg.Any<IReadOnlyList<TagEditRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SaveResult([updated], [], null));
        SaveResult? observed = null;
        _sut.Saved += (_, result) => observed = result;

        // Act
        await _sut.SaveAsync(previewRequired: true);

        // Assert
        Assert.NotNull(observed);
        Assert.Null(_sut.AllFields.First(x => x.Field == TagField.Title).Error);
        Assert.False(_sut.HasChanges);
        await _saveService.Received(1).ApplyAsync(
            Arg.Is<IReadOnlyList<TagEditRequest>>(x => x.Count == 1),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_ShouldStop_WhenValidationFails()
    {
        // Arrange
        _sut.LoadSelection([TestTrack.Create()]);
        _sut.AllFields.First(x => x.Field == TagField.Year).Value = "not-a-year";

        // Act
        await _sut.SaveAsync(previewRequired: false);

        // Assert
        await _saveService.DidNotReceiveWithAnyArgs().ApplyAsync(default!, default!, default);
        Assert.Contains("Year", _sut.StatusMessage);
    }

    [Fact]
    public async Task ChooseArtworkAsync_ShouldStageArtwork_WhenImageIsValid()
    {
        // Arrange
        var artwork = new CoverArt([1, 2, 3], "image/png");
        _sut.LoadSelection([TestTrack.Create()]);
        _dialogs.PickArtworkAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<byte[]?>(artwork.Data));
        _coverArt.Create(artwork.Data, null).Returns(CoverArtResult.Ok(artwork));

        // Act
        await _sut.ChooseArtworkAsync();

        // Assert
        Assert.Equal(artwork.Data, _sut.ArtworkBytes);
        Assert.True(_sut.HasChanges);
        Assert.Contains("image/png", _sut.ArtworkSummary);
    }

    [Fact]
    public void RemoveArtwork_ShouldStageRemoval_WhenSelectionExists()
    {
        // Arrange
        _sut.LoadSelection([TestTrack.Create(artwork: new CoverArt([1, 2, 3], "image/png"))]);

        // Act
        _sut.RemoveArtwork();

        // Assert
        Assert.Null(_sut.ArtworkBytes);
        Assert.True(_sut.HasChanges);
        Assert.Contains("removed", _sut.ArtworkSummary);
    }
}
