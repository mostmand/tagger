using System.Text.Json;
using System.Text.Json.Nodes;
using Tagger.Mcp.Tools;

namespace Tagger.Mcp;

/// <summary>Newline-delimited JSON-RPC server implementing the MCP stdio transport.</summary>
public sealed class McpServerHost
{
    public const string ProtocolVersion = "2024-11-05";

    private readonly McpToolRegistry _registry;

    public McpServerHost(McpToolRegistry registry)
    {
        _registry = registry;
    }

    public async Task<int> RunAsync(
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await HandleLineAsync(line, output, cancellationToken);
        }

        return 0;
    }

    private async Task HandleLineAsync(string line, TextWriter output, CancellationToken cancellationToken)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            await WriteAsync(output, null, null, -32700, "Parse error");
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            var id = root.TryGetProperty("id", out var idElement)
                ? JsonNode.Parse(idElement.GetRawText())
                : null;

            if (!root.TryGetProperty("method", out var methodElement) ||
                methodElement.ValueKind != JsonValueKind.String)
            {
                await WriteAsync(output, id, null, -32600, "Invalid request");
                return;
            }

            var method = methodElement.GetString()!;
            if (method.StartsWith("notifications/", StringComparison.Ordinal))
            {
                return;
            }

            var arguments = root.TryGetProperty("params", out var paramsElement)
                ? paramsElement
                : default;

            try
            {
                var result = await DispatchAsync(method, arguments, cancellationToken);
                await WriteAsync(output, id, result, null, null);
            }
            catch (McpToolException ex)
            {
                await WriteAsync(output, id, null, -32602, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                await WriteAsync(output, id, null, -32001, ex.Message);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                await WriteAsync(output, id, null, -32603, ex.Message);
            }
        }
    }

    private async Task<JsonNode> DispatchAsync(
        string method,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        switch (method)
        {
            case "initialize":
                return new JsonObject
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["capabilities"] = new JsonObject
                    {
                        ["tools"] = new JsonObject()
                    },
                    ["serverInfo"] = new JsonObject
                    {
                        ["name"] = "tagger",
                        ["version"] = "0.1.0"
                    }
                };

            case "ping":
                return new JsonObject();

            case "tools/list":
                return new JsonObject { ["tools"] = _registry.Describe() };

            case "tools/call":
            {
                var name = arguments.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;
                var toolArguments = arguments.TryGetProperty("arguments", out var argsElement)
                    ? argsElement
                    : default;

                var result = await _registry.InvokeAsync(name, toolArguments, cancellationToken);
                return new JsonObject
                {
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = result.ToJsonString()
                        }
                    },
                    ["isError"] = false
                };
            }

            default:
                throw new McpToolException($"Method '{method}' is not supported.");
        }
    }

    private static async Task WriteAsync(
        TextWriter output,
        JsonNode? id,
        JsonNode? result,
        int? errorCode,
        string? errorMessage)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id
        };

        if (errorCode is not null)
        {
            response["error"] = new JsonObject
            {
                ["code"] = errorCode,
                ["message"] = errorMessage
            };
        }
        else
        {
            response["result"] = result;
        }

        await output.WriteLineAsync(response.ToJsonString());
        await output.FlushAsync();
    }
}
