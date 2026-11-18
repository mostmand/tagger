using Tagger.Desktop.ViewModels;
using Xunit;

namespace Tagger.Desktop.Tests;

public sealed class FolderLabelFormatterTests
{
    private readonly FolderLabelFormatter _sut = new(["music"]);

    [Fact]
    public void Format_ShouldReturnFolderName_WhenDirectoryIsTheRoot()
    {
        // Arrange

        // Act
        var label = _sut.Format("music");

        // Assert
        Assert.Equal("music", label);
    }

    [Fact]
    public void Format_ShouldReturnRelativePath_WhenDirectoryIsNestedUnderRoot()
    {
        // Arrange
        var directory = Path.Combine("music", "Aurora", "All My Demons");

        // Act
        var label = _sut.Format(directory);

        // Assert
        Assert.Equal(Path.Combine("Aurora", "All My Demons"), label);
    }

    [Fact]
    public void Format_ShouldPrefixRootName_WhenMultipleRootsAreLoaded()
    {
        // Arrange
        var other = Path.Combine("archive", "Singles");
        var sut = new FolderLabelFormatter(["music", "archive"]);

        // Act
        var label = sut.Format(other);

        // Assert
        Assert.Equal(Path.Combine("archive", "Singles"), label);
    }

    [Fact]
    public void Format_ShouldReturnFullPath_WhenDirectoryIsOutsideEveryRoot()
    {
        // Arrange
        var directory = Path.Combine("elsewhere", "Loose track");

        // Act
        var label = _sut.Format(directory);

        // Assert
        Assert.Equal(directory, label);
    }

    [Fact]
    public void Format_ShouldReturnEmpty_WhenDirectoryIsEmpty()
    {
        // Arrange

        // Act
        var label = _sut.Format(string.Empty);

        // Assert
        Assert.Equal(string.Empty, label);
    }
}
