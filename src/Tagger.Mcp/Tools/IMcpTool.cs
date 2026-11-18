using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tagger.Mcp.Tools;

public interface IMcpTool
{
    string Name { get; }

    string Description { get; }

    JsonObject InputSchema { get; }

    Task<JsonNode> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken = default);
}

internal static class ToolArgs
{
    public static string RequireString(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new McpToolException($"Argument '{name}' is required.");
        }

        return value.GetString()!;
    }

    public static string? OptionalString(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    public static bool GetBool(JsonElement arguments, string name, bool defaultValue = false)
    {
        if (!arguments.TryGetProperty(name, out var value))
        {
            return defaultValue;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }

    public static IReadOnlyList<string> GetStringArray(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value))
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return [value.GetString()!];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    public static IReadOnlyDictionary<string, string?> GetFields(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("fields", out var fields) ||
            fields.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string?>();
        }

        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in fields.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => property.Value.GetString(),
                _ => property.Value.ToString()
            };
        }

        return result;
    }
}

public sealed class McpToolException : Exception
{
    public McpToolException(string message) : base(message)
    {
    }
}
