using System.Text.Json;
using System.Text.Json.Nodes;
using Tagger.Application.Abstractions;
using Tagger.Application.Editing;
using Tagger.Domain.Tracks;
using Tagger.Mcp.Security;

namespace Tagger.Mcp.Tools;

public sealed class TagsDiffTool : IMcpTool
{
    private readonly ITrackReader _reader;
    private readonly IChangePreviewService _preview;
    private readonly PathGuard _guard;

    public TagsDiffTool(ITrackReader reader, IChangePreviewService preview, PathGuard guard)
    {
        _reader = reader;
        _preview = preview;
        _guard = guard;
    }

    public string Name => "tags_diff";

    public string Description => "Return the exact field-level diff a tag patch would produce without writing.";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["paths"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" }
            },
            ["fields"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = true }
        },
        ["required"] = new JsonArray("paths", "fields")
    };

    public async Task<JsonNode> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var paths = ToolArgs.GetStringArray(arguments, "paths");
        var changes = TagsWriteTool.BuildChanges(ToolArgs.GetFields(arguments));
        var tracks = new List<Track>();

        foreach (var path in paths)
        {
            tracks.Add(await _reader.ReadAsync(_guard.EnsureAllowed(path), cancellationToken));
        }

        var preview = _preview.Create(tracks, changes);
        return new JsonObject
        {
            ["hasChanges"] = preview.HasChanges,
            ["diff"] = TagsWriteTool.DiffJson(preview)
        };
    }
}
