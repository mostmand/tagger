using System.Text.Json;
using System.Text.Json.Nodes;
using Tagger.Application.Abstractions;
using Tagger.Application.Library;
using Tagger.Domain.Naming;
using Tagger.Mcp.Security;

namespace Tagger.Mcp.Tools;

public sealed class LibraryScanTool : IMcpTool
{
    private readonly ILibraryService _library;
    private readonly PathGuard _guard;

    public LibraryScanTool(ILibraryService library, PathGuard guard)
    {
        _library = library;
        _guard = guard;
    }

    public string Name => "library_scan";

    public string Description => "Scan a directory and report formats, tag coverage, and unreadable files.";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject { ["type"] = "string" },
            ["recursive"] = new JsonObject { ["type"] = "boolean", ["default"] = true }
        },
        ["required"] = new JsonArray("path")
    };

    public async Task<JsonNode> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var path = _guard.EnsureAllowed(ToolArgs.RequireString(arguments, "path"));
        var recursive = ToolArgs.GetBool(arguments, "recursive", true);
        var result = await _library.LoadAsync([path], recursive, cancellationToken: cancellationToken);

        var formats = result.Tracks
            .GroupBy(x => x.Format)
            .ToDictionary(x => x.Key, x => x.Count());

        var missingTitle = result.Tracks.Count(x => !x.Tags.Contains(Domain.Tracks.TagField.Title));
        var missingArtist = result.Tracks.Count(x => !x.Tags.Contains(Domain.Tracks.TagField.Artist));
        var missingAlbum = result.Tracks.Count(x => !x.Tags.Contains(Domain.Tracks.TagField.Album));

        return new JsonObject
        {
            ["path"] = path,
            ["scanned"] = result.ScannedFileCount,
            ["loaded"] = result.Tracks.Count,
            ["failures"] = TrackJson.Failures(result.Failures),
            ["formats"] = JsonSerializer.SerializeToNode(formats),
            ["missing"] = new JsonObject
            {
                ["title"] = missingTitle,
                ["artist"] = missingArtist,
                ["album"] = missingAlbum
            }
        };
    }
}
