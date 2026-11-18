using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tagger.Mcp;

public sealed class McpToolRegistry
{
    private readonly Dictionary<string, Tools.IMcpTool> _tools;

    public McpToolRegistry(IEnumerable<Tools.IMcpTool> tools)
    {
        _tools = tools.ToDictionary(x => x.Name, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<Tools.IMcpTool> Tools => _tools.Values;

    public JsonArray Describe()
    {
        var array = new JsonArray();
        foreach (var tool in _tools.Values.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            array.Add(new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.InputSchema
            });
        }

        return array;
    }

    public async Task<JsonNode> InvokeAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(name, out var tool))
        {
            throw new Tools.McpToolException($"Unknown tool '{name}'.");
        }

        return await tool.InvokeAsync(arguments, cancellationToken);
    }
}
