using System.Text.Json;
using System.Text.Json.Nodes;
using Tagger.Application.Abstractions;
using Tagger.Application.Renaming;
using Tagger.Domain.Naming;
using Tagger.Domain.Tracks;
using Tagger.Mcp.Security;

namespace Tagger.Mcp.Tools;

public sealed class FilesRenameTool : IMcpTool
{
    private readonly IFileScanner _scanner;
    private readonly ITrackReader _reader;
    private readonly IRenameService _rename;
    private readonly PathGuard _guard;

    public FilesRenameTool(
        IFileScanner scanner,
        ITrackReader reader,
        IRenameService rename,
        PathGuard guard)
    {
        _scanner = scanner;
        _reader = reader;
        _rename = rename;
        _guard = guard;
    }

    public string Name => "files_rename";

    public string Description =>
        "Preview or commit renames derived from a tag pattern. Collisions are rejected.";

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
            throw new McpToolException(error ?? "Invalid rename pattern.");
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

        var plan = _rename.CreatePlan(tracks, pattern);
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

        var result = await _rename.ExecuteAsync(plan, "MCP rename", cancellationToken);
        return new JsonObject
        {
            ["dryRun"] = false,
            ["renamed"] = result.UpdatedTracks.Count,
            ["failures"] = TrackJson.Failures(result.Failures),
            ["transactionId"] = result.Transaction?.Id.ToString()
        };
    }

    private static JsonArray PlanJson(RenamePlan plan)
    {
        var array = new JsonArray();
        foreach (var entry in plan.Entries)
        {
            array.Add(new JsonObject
            {
                ["from"] = entry.OriginalPath,
                ["to"] = entry.TargetPath,
                ["valid"] = entry.IsValid,
                ["noOp"] = entry.IsNoOp,
                ["error"] = entry.Error
            });
        }

        return array;
    }
}
