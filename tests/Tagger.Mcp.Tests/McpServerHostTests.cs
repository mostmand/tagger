using System.Text.Json;
using System.Text.Json.Nodes;
using Tagger.Mcp;
using Tagger.Mcp.Tools;
using Xunit;

namespace Tagger.Mcp.Tests;

public sealed class McpServerHostTests
{
    [Fact]
    public async Task RunAsync_ShouldRespondToInitialize_WhenRequestIsValid()
    {
        // Arrange
        var registry = new McpToolRegistry([new FakeTool()]);
        var _sut = new McpServerHost(registry);
        var input = new StringReader("""
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """);
        var output = new StringWriter();

        // Act
        await _sut.RunAsync(input, output);

        // Assert
        using var response = JsonDocument.Parse(output.ToString().Trim());
        Assert.Equal(1, response.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("tagger", response.RootElement
            .GetProperty("result")
            .GetProperty("serverInfo")
            .GetProperty("name")
            .GetString());
    }

    [Fact]
    public async Task RunAsync_ShouldListRegisteredTools_WhenToolsListRequested()
    {
        // Arrange
        var registry = new McpToolRegistry([new FakeTool()]);
        var _sut = new McpServerHost(registry);
        var input = new StringReader("""
            {"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}
            """);
        var output = new StringWriter();

        // Act
        await _sut.RunAsync(input, output);

        // Assert
        using var response = JsonDocument.Parse(output.ToString().Trim());
        var tools = response.RootElement.GetProperty("result").GetProperty("tools");
        Assert.Single(tools.EnumerateArray());
        Assert.Equal("fake_tool", tools[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task RunAsync_ShouldInvokeTool_WhenToolsCallRequested()
    {
        // Arrange
        var registry = new McpToolRegistry([new FakeTool()]);
        var _sut = new McpServerHost(registry);
        var input = new StringReader("""
            {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"fake_tool","arguments":{"value":"hello"}}}
            """);
        var output = new StringWriter();

        // Act
        await _sut.RunAsync(input, output);

        // Assert
        using var response = JsonDocument.Parse(output.ToString().Trim());
        var text = response.RootElement
            .GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();
        Assert.Contains("hello", text);
    }

    [Fact]
    public async Task RunAsync_ShouldReturnError_WhenToolIsUnknown()
    {
        // Arrange
        var registry = new McpToolRegistry([]);
        var _sut = new McpServerHost(registry);
        var input = new StringReader("""
            {"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"missing","arguments":{}}}
            """);
        var output = new StringWriter();

        // Act
        await _sut.RunAsync(input, output);

        // Assert
        using var response = JsonDocument.Parse(output.ToString().Trim());
        Assert.Equal(-32602, response.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    private sealed class FakeTool : IMcpTool
    {
        public string Name => "fake_tool";

        public string Description => "A test tool.";

        public JsonObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["value"] = new JsonObject { ["type"] = "string" }
            }
        };

        public Task<JsonNode> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken = default)
        {
            var value = arguments.TryGetProperty("value", out var element)
                ? element.GetString()
                : null;

            return Task.FromResult<JsonNode>(new JsonObject { ["echo"] = value });
        }
    }
}
