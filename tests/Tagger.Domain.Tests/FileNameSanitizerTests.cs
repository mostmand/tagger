using Tagger.Domain.Naming;
using Xunit;

namespace Tagger.Domain.Tests;

public sealed class FileNameSanitizerTests
{
    [Fact]
    public void SanitizeSegment_ShouldReplaceInvalidCharacters_WhenPresent()
    {
        // Arrange
        var _sut = FileNameSanitizer.SanitizeSegment;

        // Act
        var result = _sut("Aurora: Northern / Lights?");

        // Assert
        Assert.Equal("Aurora_ Northern _ Lights_", result);
    }

    [Fact]
    public void SanitizeSegment_ShouldPrefixReservedName_WhenWindowsReserved()
    {
        // Arrange
        var _sut = FileNameSanitizer.SanitizeSegment;

        // Act
        var result = _sut("CON");

        // Assert
        Assert.Equal("_CON", result);
    }

    [Fact]
    public void SanitizeRelativePath_ShouldRemoveTraversalSegments_WhenPresent()
    {
        // Arrange
        var _sut = FileNameSanitizer.SanitizeRelativePath;

        // Act
        var result = _sut("../../Aurora/../Northern Lights");

        // Assert
        Assert.Equal("Aurora/Northern Lights", result);
    }

    [Fact]
    public void SanitizeSegment_ShouldTrimDotsAndSpaces_WhenValueHasThem()
    {
        // Arrange
        var _sut = FileNameSanitizer.SanitizeSegment;

        // Act
        var result = _sut("  Album.  ");

        // Assert
        Assert.Equal("Album", result);
    }
}
