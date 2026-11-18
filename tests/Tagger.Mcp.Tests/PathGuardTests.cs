using Tagger.Mcp.Security;
using Xunit;

namespace Tagger.Mcp.Tests;

public sealed class PathGuardTests
{
    [Fact]
    public void IsAllowed_ShouldAcceptPath_WhenInsideConfiguredRoot()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "tagger-mcp-root");
        var _sut = new PathGuard([root]);

        // Act
        var allowed = _sut.IsAllowed(Path.Combine(root, "album", "song.mp3"));

        // Assert
        Assert.True(allowed);
    }

    [Fact]
    public void IsAllowed_ShouldRejectPath_WhenOutsideConfiguredRoot()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "tagger-mcp-root");
        var _sut = new PathGuard([root]);

        // Act
        var allowed = _sut.IsAllowed(Path.Combine(Path.GetTempPath(), "elsewhere", "song.mp3"));

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void IsAllowed_ShouldRejectPrefixLookalike_WhenRootIsSibling()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "music");
        var _sut = new PathGuard([root]);

        // Act
        var allowed = _sut.IsAllowed(Path.Combine(Path.GetTempPath(), "music2", "song.mp3"));

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void EnsureAllowed_ShouldThrow_WhenPathIsNotPermitted()
    {
        // Arrange
        var _sut = new PathGuard([Path.Combine(Path.GetTempPath(), "music")]);

        // Act
        var exception = Assert.Throws<UnauthorizedAccessException>(
            () => _sut.EnsureAllowed(Path.Combine(Path.GetTempPath(), "private", "song.mp3")));

        // Assert
        Assert.Contains("allowed", exception.Message);
    }

    [Fact]
    public void IsAllowed_ShouldRejectEverything_WhenNoRootsAreConfigured()
    {
        // Arrange
        var _sut = new PathGuard([]);

        // Act
        var allowed = _sut.IsAllowed(Path.Combine(Path.GetTempPath(), "song.mp3"));

        // Assert
        Assert.False(allowed);
    }
}
