using System.Text.Json;
using Tagger.Mcp.Tools;
using Xunit;

namespace Tagger.Mcp.Tests;

public sealed class TagsWriteToolTests
{
    [Fact]
    public void BuildChanges_ShouldThrow_WhenFieldTokenIsUnknown()
    {
        // Arrange
        var _sut = TagsWriteTool.BuildChanges;
        var fields = new Dictionary<string, string?> { ["mystery"] = "value" };

        // Act
        var exception = Assert.Throws<McpToolException>(() => _sut(fields));

        // Assert
        Assert.Contains("mystery", exception.Message);
    }

    [Fact]
    public void BuildChanges_ShouldMapTokens_WhenFieldsAreKnown()
    {
        // Arrange
        var _sut = TagsWriteTool.BuildChanges;
        var fields = new Dictionary<string, string?>
        {
            ["title"] = "Northern Lights",
            ["album"] = null
        };

        // Act
        var result = _sut(fields);

        // Assert
        Assert.Equal("Northern Lights", result.Get(Domain.Tracks.TagField.Title));
        Assert.Null(result.Get(Domain.Tracks.TagField.Album));
    }
}
