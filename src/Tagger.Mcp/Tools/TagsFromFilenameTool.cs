using System.Text.Json;
using System.Text.Json.Nodes;
using Tagger.Application.Abstractions;
using Tagger.Application.Editing;
using Tagger.Application.Importing;
using Tagger.Domain.Naming;
using Tagger.Domain.Tracks;
using Tagger.Mcp.Security;

namespace Tagger.Mcp.Tools;

public sealed class TagsFromFilenameTool : IMcpTool
{
    private readonly IFileScanner _scanner;
    private readonly ITrackReader _reader;
    private readonly IFilenameImportService _import;
    private readonly ITagSaveService _save;
    private readonly PathGuard _guard;

    public TagsFromFilenameTool(
        IFileScanner scanner,
        ITrackReader reader,
        IFilenameImportService import,
        ITagSaveService save,
        PathGuard guard)
    {
        _scanner = scanner;
        _reader = reader;
        _import = import;
        _save = save;
        _guard = guard;
    }

    public string Name => "tags_from_filename";

    public string Description =>
        "Parse tags from filenames using a pattern. Preview first; commit=true writes the parsed tags.";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject { ["type"] = "string" },
            ["pattern"] = new JsonObject { ["type"] = "string" },
            ["recursive"] = new JsonObject { ["type"] = "boolean", ["default"] = true },
            ["commit"] = new JsonObject { ["type"] = "boolean", ["default"] = false }
        },
        ["required"] = new JsonArray("path", "pattern")
    };

    public async Task<JsonNode> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var path = _guard.EnsureAllowed(ToolArgs.RequireString(arguments, "path"));
        var patternText = ToolArgs.RequireString(arguments, "pattern");
        if (!TagPattern.TryParse(patternText, out var pattern, out var error) || pattern is null)
        {
            throw new McpToolException(error ?? "Invalid filename pattern.");
        }

        var recursive = ToolArgs.GetBool(arguments, "recursive", true);
        var files = File.Exists(path)
            ? [path]
            : await _scanner.FindAudioFilesAsync([path], recursive, cancellationToken);

        var tracks = new List<Track>();
        foreach (var file in files)
        {
            tracks.Add(await _reader.ReadAsync(file, cancellationToken));
        }

        var plan = _import.CreatePlan(tracks, pattern);
        var commit = ToolArgs.GetBool(arguments, "commit");
        if (!commit)
        {
            return new JsonObject
            {
                ["dryRun"] = true,
                ["changes"] = plan.ChangeCount,
                ["errors"] = plan.ErrorCount,
                ["entries"] = PlanJson(plan)
            };
        }

        var requests = plan.Entries
            .Where(x => x.IsValid && x.HasChanges)
            .Select(x => new TagEditRequest(x.Track, x.Changes))
            .ToList();

        var result = await _save.ApplyAsync(requests, "MCP tags from filename", cancellationToken);
        return new JsonObject
        {
            ["dryRun"] = false,
            ["updated"] = result.UpdatedTracks.Count,
            ["failures"] = TrackJson.Failures(result.Failures)
        };
    }

    private static JsonArray PlanJson(FilenameImportPlan plan)
    {
        var array = new JsonArray();
        foreach (var entry in plan.Entries)
        {
            array.Add(new JsonObject
            {
                ["path"] = entry.Track.FilePath,
                ["valid"] = entry.IsValid,
                ["error"] = entry.Error,
                ["changes"] = new JsonObject(
                    entry.Changes.Values.Select(x => new KeyValuePair<string, JsonNode?>(
                        TagFieldCatalog.Get(x.Key).Token,
                        x.Value is null ? null : JsonValue.Create(x.Value))))
            });
        }

        return array;
    }
}
