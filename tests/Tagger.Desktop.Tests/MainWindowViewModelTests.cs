using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using Tagger.Application.Abstractions;
using Tagger.Application.Artwork;
using Tagger.Application.Duplicates;
using Tagger.Application.Editing;
using Tagger.Application.Journal;
using Tagger.Application.Library;
using Tagger.Application.Settings;
using Tagger.Domain.Tracks;
using Tagger.Desktop.Services;
using Tagger.Desktop.ViewModels;
using Xunit;

namespace Tagger.Desktop.Tests;

public sealed class MainWindowViewModelTests
{
    private readonly ILibraryService _library = Substitute.For<ILibraryService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IThemeService _theme = Substitute.For<IThemeService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IUndoService _undo = Substitute.For<IUndoService>();
    private readonly IDuplicateFinder _duplicates = Substitute.For<IDuplicateFinder>();
    private readonly ITagSaveService _saveService = Substitute.For<ITagSaveService>();
    private readonly ICoverArtService _coverArt = Substitute.For<ICoverArtService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly TagEditorViewModel _editor;
    private readonly MainWindowViewModel _sut;

    public MainWindowViewModelTests()
    {
        _clock.UtcNow.Returns(new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero));
        _settingsService.LoadAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);
        _editor = new TagEditorViewModel(
            new SelectionSummaryService(),
            _saveService,
            new ChangeSetPreviewService(),
            _coverArt,
            _dialogs,
            _clock);
        _sut = new MainWindowViewModel(
            _library,
            _settingsService,
            _theme,
            _dialogs,
            _undo,
            _duplicates,
            _editor);
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadSettingsAndApplyTheme_WhenStarted()
    {
        // Arrange
        var settings = AppSettings.Default with { Theme = AppTheme.Light };
        _settingsService.LoadAsync(Arg.Any<CancellationToken>()).Returns(settings);

        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.Equal(AppTheme.Light, _sut.Settings.Theme);
        _theme.Received(1).Apply(AppTheme.Light);
        Assert.Contains("Add a folder", _sut.StatusMessage);
    }

    [Fact]
    public async Task LoadPathsAsync_ShouldPopulateVisibleTracks_WhenLibraryReturnsTracks()
    {
        // Arrange
        var track = TestTrack.Create("Aurora - Northern Lights.mp3", title: "Northern Lights", artist: "Aurora");
        _library.LoadAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>(), Arg.Any<IProgress<LibraryProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new LibraryLoadResult([track], [], 1));

        // Act
        await _sut.LoadPathsAsync(["music"]);

        // Assert
        Assert.Single(_sut.VisibleTracks);
        Assert.Equal("Northern Lights", _sut.VisibleTracks[0].Title);
        Assert.True(_sut.HasTracks);
        Assert.False(_sut.IsEmpty);
    }

    [Fact]
    public async Task SearchText_ShouldFilterVisibleTracks_WhenQueryChanges()
    {
        // Arrange
        var first = TestTrack.Create("one.mp3", title: "Northern Lights", artist: "Aurora");
        var second = TestTrack.Create("two.mp3", title: "Ember & Ash", artist: "Aurora");
        _library.LoadAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>(), Arg.Any<IProgress<LibraryProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new LibraryLoadResult([first, second], [], 2));
        await _sut.LoadPathsAsync(["music"]);

        // Act
        _sut.SearchText = "ember";

        // Assert
        Assert.Single(_sut.VisibleTracks);
        Assert.Equal("Ember & Ash", _sut.VisibleTracks[0].Title);
    }

    [Fact]
    public async Task SetSelection_ShouldLoadEditor_WhenRowsAreSelected()
    {
        // Arrange
        var track = TestTrack.Create("one.mp3", title: "Northern Lights", artist: "Aurora");
        _library.LoadAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>(), Arg.Any<IProgress<LibraryProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new LibraryLoadResult([track], [], 1));
        await _sut.LoadPathsAsync(["music"]);

        // Act
        _sut.SetSelection([_sut.VisibleTracks[0]]);

        // Assert
        Assert.True(_sut.HasSelection);
        Assert.Equal(1, _sut.SelectionCount);
        Assert.Equal("Northern Lights", _editor.AllFields.First(x => x.Field == TagField.Title).Value);
    }

    [Fact]
    public async Task SaveCommand_ShouldApplyUpdatedRow_WhenEditorSaves()
    {
        // Arrange
        var track = TestTrack.Create("one.mp3", title: "Old", artist: "Aurora");
        _library.LoadAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>(), Arg.Any<IProgress<LibraryProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new LibraryLoadResult([track], [], 1));
        await _sut.LoadPathsAsync(["music"]);
        _sut.SetSelection([_sut.VisibleTracks[0]]);
        _editor.AllFields.First(x => x.Field == TagField.Title).Value = "New";
        _dialogs.ShowChangePreviewAsync(Arg.Any<ChangePreview>(), Arg.Any<string>()).Returns(true);
        _saveService.ApplyAsync(Arg.Any<IReadOnlyList<TagEditRequest>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SaveResult([track.WithTags(track.Tags.With(TagField.Title, "New"))], [], null));

        // Act
        await _sut.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("New", _sut.VisibleTracks[0].Title);
    }

    [Fact]
    public async Task RemoveSelectionCommand_ShouldUnloadRows_WhenSelectionExists()
    {
        // Arrange
        var track = TestTrack.Create("one.mp3");
        _library.LoadAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>(), Arg.Any<IProgress<LibraryProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new LibraryLoadResult([track], [], 1));
        await _sut.LoadPathsAsync(["music"]);
        _sut.SetSelection([_sut.VisibleTracks[0]]);

        // Act
        _sut.RemoveSelectionCommand.Execute(null);

        // Assert
        Assert.Empty(_sut.VisibleTracks);
        Assert.False(_sut.HasTracks);
        Assert.False(_sut.HasSelection);
    }

    [Fact]
    public async Task SortCommand_ShouldToggleDirection_WhenSameFieldClickedTwice()
    {
        // Arrange
        var first = TestTrack.Create("one.mp3", title: "Alpha");
        var second = TestTrack.Create("two.mp3", title: "Zulu");
        _library.LoadAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>(), Arg.Any<IProgress<LibraryProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new LibraryLoadResult([first, second], [], 2));
        await _sut.LoadPathsAsync(["music"]);

        // Act
        _sut.SortCommand.Execute("Title");
        _sut.SortCommand.Execute("Title");

        // Assert
        Assert.True(_sut.SortDescending);
        Assert.Equal("Zulu", _sut.VisibleTracks[0].Title);
    }
}
