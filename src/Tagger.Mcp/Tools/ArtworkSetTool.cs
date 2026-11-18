using System.Text.Json;
using System.Text.Json.Nodes;
using Tagger.Application.Abstractions;
using Tagger.Application.Artwork;
using Tagger.Application.Editing;
using Tagger.Domain.Tracks;
using Tagger.Mcp.Security;

namespace Tagger.Mcp.Tools;

public sealed class ArtworkSetTool : IMcpTool
{
    private readonly ITrackReader _reader;
    private readonly IFileSystem _fileSystem;
    private readonly ICoverArtService _coverArt;
    private readonly ITagSaveService _save;
    private readonly PathGuard _guard;

    public ArtworkSetTool(
        ITrackReader reader,
        IFileSystem fileSystem,
        ICoverArtService coverArt,
        ITagSaveService save,
        PathGuard guard)
    {
        _reader = reader;
        _fileSystem = fileSystem;
        _coverArt = coverArt;
        _save = save;
        _guard = guard;
    }

    public string Name => "artwork_set";

    public string Description => "Preview or commit embedded cover artwork for a file.";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject { ["type"] = "string" },
            ["imagePath"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Image file to embed. Omit when remove=true."
            },
            ["remove"] = new JsonObject { ["type"] = "boolean", ["default"] = false },
            ["commit"] = new JsonObject { ["type"] = "boolean", ["default"] = false }
        },
        ["required"] = new JsonArray("path")
    };

    public async Task<JsonNode> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var path = _guard.EnsureAllowed(ToolArgs.RequireString(arguments, "path"));
        var remove = ToolArgs.GetBool(arguments, "remove");
        var commit = ToolArgs.GetBool(arguments, "commit");
        var track = await _reader.ReadAsync(path, cancellationToken);

        CoverArtUpdate update;
        if (remove)
        {
            update = CoverArtUpdate.Remove;
        }
        else
        {
            var imagePath = _guard.EnsureAllowed(ToolArgs.RequireString(arguments, "imagePath"));
            var data = await _fileSystem.ReadAllBytesAsync(imagePath, cancellationToken);
            var created = _coverArt.Create(data);
            if (!created.Success || created.Artwork is null)
            {
                throw new McpToolException(created.Error ?? "Artwork could not be read.");
            }

            update = CoverArtUpdate.Set(created.Artwork);
        }

        if (!commit)
        {
            return new JsonObject
            {
                ["dryRun"] = true,
                ["path"] = track.FilePath,
                ["action"] = remove ? "remove" : "replace",
                ["hasExistingArtwork"] = track.CoverArt is not null
            };
        }

        var result = await _save.ApplyAsync(
            [new TagEditRequest(track, TagChangeSet.Empty, update)],
            "MCP artwork update",
            cancellationToken);

        return new JsonObject
        {
            ["dryRun"] = false,
            ["updated"] = result.UpdatedTracks.Count,
            ["failures"] = TrackJson.Failures(result.Failures)
        };
    }
}
