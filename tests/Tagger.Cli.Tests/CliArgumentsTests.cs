using Tagger.Cli;
using Xunit;

namespace Tagger.Cli.Tests;

public sealed class CliArgumentsTests
{
    [Fact]
    public void Parse_ShouldReadCommandPositionalsAndOptions_WhenAllArePresent()
    {
        // Arrange
        var _sut = CliArguments.Parse;

        // Act
        var result = _sut(["write", "album", "--album", "Static Bloom", "--year=2026"]);

        // Assert
        Assert.Equal("write", result.Command);
        Assert.Equal(["album"], result.Positionals);
        Assert.Equal("Static Bloom", result.Get("album"));
        Assert.Equal("2026", result.Get("year"));
    }

    [Fact]
    public void GetBool_ShouldBeTrue_WhenFlagHasNoValue()
    {
        // Arrange
        var _sut = CliArguments.Parse;

        // Act
        var result = _sut(["list", "music", "--recursive"]);

        // Assert
        Assert.True(result.GetBool("recursive"));
    }

    [Fact]
    public void GetBool_ShouldBeFalse_WhenExplicitlyFalse()
    {
        // Arrange
        var _sut = CliArguments.Parse;

        // Act
        var result = _sut(["list", "music", "--recursive=false"]);

        // Assert
        Assert.False(result.GetBool("recursive"));
    }

    [Fact]
    public void Parse_ShouldSupportRepeatedOptions_WhenUsed()
    {
        // Arrange
        var _sut = CliArguments.Parse;

        // Act
        var result = _sut(["mcp", "--allow", "/music", "--allow", "/audio"]);

        // Assert
        Assert.Equal(["/music", "/audio"], result.GetAll("allow"));
    }

    [Fact]
    public void Parse_ShouldReturnHelp_WhenNoArgumentsProvided()
    {
        // Arrange
        var _sut = CliArguments.Parse;

        // Act
        var result = _sut([]);

        // Assert
        Assert.Equal("help", result.Command);
        Assert.Empty(result.Positionals);
    }
}
