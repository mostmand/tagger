using System.Text.Json;
using System.Text.Json.Nodes;
using Tagger.Application.Abstractions;
using Tagger.Mcp.Security;

namespace Tagger.Mcp.Tools;

public sealed class TagsReadTool : IMcpTool
{
    private readonly ITrackReader _reader;
    private readonly PathGuard _guard;

    public TagsReadTool(ITrackReader reader, PathGuard guard)
    {
        _reader = reader;
        _guard = guard;
    }

    public string Name => "tags_read";

    public string Description => "Read tags and audio properties for one or more files.";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["paths"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["description"] = "Absolute file paths inside an allowed root."
            }
        },
        ["required"] = new JsonArray("paths")
    };

    public async Task<JsonNode> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var paths = ToolArgs.GetStringArray(arguments, "paths");
        if (paths.Count == 0)
        {
            throw new McpToolException("At least one path is required.");
        }

        var results = new JsonArray();
        foreach (var path in paths)
        {
            var safe = _guard.EnsureAllowed(path);
            var track = await _reader.ReadAsync(safe, cancellationToken);
            results.Add(TrackJson.Track(track));
        }

        return new JsonObject { ["tracks"] = results };
    }
}
