using Tagger.Domain.Searching;
using Tagger.Desktop.ViewModels;
using Xunit;

namespace Tagger.Desktop.Tests;

public sealed class LibraryTreeBuilderTests
{
    private readonly LibraryTreeBuilder _sut = new();

    [Fact]
    public void Build_ShouldGroupTracksIntoFolderHierarchy_WhenTracksAreNested()
    {
        // Arrange
        var album = Path.Combine("music", "Aurora", "All My Demons");
        var tracks = new[]
        {
            new TrackRowViewModel(TestTrack.Create("one.mp3", folder: album), 1),
            new TrackRowViewModel(TestTrack.Create("two.mp3", folder: album), 2),
            new TrackRowViewModel(TestTrack.Create("three.mp3", folder: Path.Combine("music", "Archive")), 3)
        };

        // Act
        var roots = _sut.Build(tracks, ["music"], TrackSortField.FileName, false);

        // Assert
        var root = Assert.Single(roots);
        Assert.Equal("music", root.Name);
        Assert.Equal(3, root.TrackCount);

        var folders = root.Children.OfType<FolderNodeViewModel>().ToList();
        Assert.Equal(new[] { "Archive", "Aurora" }, folders.Select(x => x.Name).ToArray());

        var aurora = folders.Single(x => x.Name == "Aurora");
        var albumNode = Assert.Single(aurora.Children.OfType<FolderNodeViewModel>());
        Assert.Equal("All My Demons", albumNode.Name);
        Assert.Equal(2, albumNode.TrackCount);
        Assert.All(albumNode.Children, child => Assert.IsType<TrackRowViewModel>(child));
    }

    [Fact]
    public void Build_ShouldCreateSeparateRoots_WhenTracksBelongToDifferentRoots()
    {
        // Arrange
        var tracks = new[]
        {
            new TrackRowViewModel(TestTrack.Create("one.mp3", folder: "music"), 1),
            new TrackRowViewModel(TestTrack.Create("two.mp3", folder: "archive"), 2)
        };

        // Act
        var roots = _sut.Build(tracks, ["music", "archive"], TrackSortField.FileName, false);

        // Assert
        Assert.Equal(new[] { "music", "archive" }, roots.Select(x => x.Name).ToArray());
        Assert.Equal(1, roots[0].TrackCount);
        Assert.Equal(1, roots[1].TrackCount);
    }

    [Fact]
    public void Build_ShouldUseTrackFolderAsRoot_WhenFolderIsOutsideKnownRoots()
    {
        // Arrange
        var tracks = new[]
        {
            new TrackRowViewModel(TestTrack.Create("loose.mp3", folder: "elsewhere"), 1)
        };

        // Act
        var roots = _sut.Build(tracks, ["music"], TrackSortField.FileName, false);

        // Assert
        var root = Assert.Single(roots);
        Assert.Equal("elsewhere", root.Name);
        Assert.Single(root.Tracks);
    }

    [Fact]
    public void Build_ShouldSortTracksWithinFolder_WhenSortFieldIsProvided()
    {
        // Arrange
        var folder = Path.Combine("music", "Aurora");
        var tracks = new[]
        {
            new TrackRowViewModel(TestTrack.Create("zulu.mp3", title: "Zulu", folder: folder), 1),
            new TrackRowViewModel(TestTrack.Create("alpha.mp3", title: "Alpha", folder: folder), 2)
        };

        // Act
        var roots = _sut.Build(tracks, ["music"], TrackSortField.Title, false);

        // Assert
        var aurora = roots[0].Children.OfType<FolderNodeViewModel>().Single();
        Assert.Equal(new[] { "Alpha", "Zulu" }, aurora.Tracks.Select(x => x.Title).ToArray());
    }

    [Fact]
    public void Build_ShouldRestoreCollapsedState_WhenExpansionStateExists()
    {
        // Arrange
        var folder = Path.Combine("music", "Aurora");
        var tracks = new[]
        {
            new TrackRowViewModel(TestTrack.Create("one.mp3", folder: folder), 1)
        };
        var states = new Dictionary<string, bool> { [folder] = false };

        // Act
        var roots = _sut.Build(tracks, ["music"], TrackSortField.FileName, false, states);

        // Assert
        Assert.True(roots[0].IsExpanded);
        Assert.False(roots[0].Children.OfType<FolderNodeViewModel>().Single().IsExpanded);
    }
}
