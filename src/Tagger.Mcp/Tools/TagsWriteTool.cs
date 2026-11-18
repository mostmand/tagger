using System.Text.Json;
using System.Text.Json.Nodes;
using Tagger.Application.Abstractions;
using Tagger.Application.Editing;
using Tagger.Application.Library;
using Tagger.Domain.Tracks;
using Tagger.Mcp.Security;

namespace Tagger.Mcp.Tools;

public sealed class TagsWriteTool : IMcpTool
{
    private readonly ITrackReader _reader;
    private readonly IChangePreviewService _preview;
    private readonly ITagSaveService _save;
    private readonly PathGuard _guard;

    public TagsWriteTool(
        ITrackReader reader,
        IChangePreviewService preview,
        ITagSaveService save,
        PathGuard guard)
    {
        _reader = reader;
        _preview = preview;
        _save = save;
        _guard = guard;
    }

    public string Name => "tags_write";

    public string Description =>
        "Preview or apply field-level tag patches. Preview is the default; pass commit=true to write.";

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
            ["fields"] = new JsonObject
            {
                ["type"] = "object",
                ["description"] = "Map of tag token to value. null clears a field.",
                ["additionalProperties"] = true
            },
            ["commit"] = new JsonObject
            {
                ["type"] = "boolean",
                ["default"] = false,
                ["description"] = "Write the changes. When false, returns a preview only."
            }
        },
        ["required"] = new JsonArray("paths", "fields")
    };

    public async Task<JsonNode> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var paths = ToolArgs.GetStringArray(arguments, "paths");
        if (paths.Count == 0)
        {
            throw new McpToolException("At least one path is required.");
        }

        var changes = BuildChanges(ToolArgs.GetFields(arguments));
        var tracks = new List<Track>();
        foreach (var path in paths)
        {
            tracks.Add(await _reader.ReadAsync(_guard.EnsureAllowed(path), cancellationToken));
        }

        var preview = _preview.Create(tracks, changes);
        var commit = ToolArgs.GetBool(arguments, "commit");

        if (!commit)
        {
            return new JsonObject
            {
                ["dryRun"] = true,
                ["hasChanges"] = preview.HasChanges,
                ["trackCount"] = preview.ChangedTrackCount,
                ["fieldCount"] = preview.ChangedFieldCount,
                ["diff"] = DiffJson(preview)
            };
        }

        var result = await _save.ApplyAsync(tracks, changes, CoverArtUpdate.Keep, "MCP tag patch", cancellationToken);
        return new JsonObject
        {
            ["dryRun"] = false,
            ["updated"] = result.UpdatedTracks.Count,
            ["failures"] = TrackJson.Failures(result.Failures),
            ["transactionId"] = result.Transaction?.Id.ToString()
        };
    }

    public static TagChangeSet BuildChanges(IReadOnlyDictionary<string, string?> fields)
    {
        if (fields.Count == 0)
        {
            throw new McpToolException("At least one field is required.");
        }

        var changes = new TagChangeSet();
        foreach (var (token, value) in fields)
        {
            if (!TagFieldCatalog.TryGetField(token, out var field))
            {
                throw new McpToolException($"Unknown tag field '{token}'.");
            }

            changes.Add(field, value);
        }

        return changes;
    }

    internal static JsonArray DiffJson(ChangePreview preview)
    {
        var array = new JsonArray();
        foreach (var track in preview.Tracks.Where(x => x.HasChanges))
        {
            var diffs = new JsonArray();
            foreach (var diff in track.Diffs)
            {
                diffs.Add(new JsonObject
                {
                    ["field"] = TagFieldCatalog.Get(diff.Field).Token,
                    ["before"] = diff.Before,
                    ["after"] = diff.After
                });
            }

            array.Add(new JsonObject
            {
                ["path"] = track.Track.FilePath,
                ["fields"] = diffs,
                ["artworkChanged"] = track.CoverArtChanged
            });
        }

        return array;
    }
}
