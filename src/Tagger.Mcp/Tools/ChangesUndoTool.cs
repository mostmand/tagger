using System.Text.Json;
using System.Text.Json.Nodes;
using Tagger.Application.Abstractions;
using Tagger.Application.Journal;
using Tagger.Mcp.Security;

namespace Tagger.Mcp.Tools;

public sealed class ChangesUndoTool : IMcpTool
{
    private readonly IUndoService _undo;
    private readonly IChangeJournal _journal;
    private readonly PathGuard _guard;

    public ChangesUndoTool(IUndoService undo, IChangeJournal journal, PathGuard guard)
    {
        _undo = undo;
        _journal = journal;
        _guard = guard;
    }

    public string Name => "changes_undo";

    public string Description => "Undo the most recent journaled tag write or rename inside the allowed roots.";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject()
    };

    public async Task<JsonNode> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var transaction = await _journal.GetLastUndoableAsync(cancellationToken);
        if (transaction is null)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["message"] = "There is nothing to undo."
            };
        }

        var paths = transaction.TagWrites.Select(x => x.FilePath)
            .Concat(transaction.FileMoves.SelectMany(x => new[] { x.FromPath, x.ToPath }))
            .Distinct()
            .ToList();

        foreach (var path in paths)
        {
            _guard.EnsureAllowed(path);
        }

        var result = await _undo.UndoLastAsync(cancellationToken);
        return new JsonObject
        {
            ["success"] = result.Success,
            ["message"] = result.Message,
            ["transactionId"] = result.Transaction?.Id.ToString()
        };
    }
}
