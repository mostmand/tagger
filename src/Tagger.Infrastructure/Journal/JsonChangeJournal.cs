using System.Text.Json;
using System.Text.Json.Serialization;
using Tagger.Domain.Journal;
using Tagger.Domain.Tracks;

namespace Tagger.Infrastructure.Journal;

/// <summary>Append-only JSON-lines journal. One line is one undoable transaction.</summary>
public sealed class JsonChangeJournal : Application.Abstractions.IChangeJournal
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(),
            new TrackTagsJsonConverter()
        }
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonChangeJournal(string? filePath = null)
    {
        _filePath = filePath ?? DefaultPath;
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "tagger",
        "journal.jsonl");

    public async Task RecordAsync(
        ChangeTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var line = JsonSerializer.Serialize(transaction, Options);
            await File.AppendAllTextAsync(_filePath, line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChangeTransaction?> GetLastUndoableAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var transactions = await ReadAllAsync(cancellationToken);
            return transactions.LastOrDefault(x => x.CanUndo);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkUndoneAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var transactions = await ReadAllAsync(cancellationToken);
            var index = transactions.FindIndex(x => x.Id == transactionId);
            if (index < 0)
            {
                return;
            }

            transactions[index] = transactions[index].MarkUndone();
            await WriteAllAsync(transactions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<ChangeTransaction>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync(_filePath, cancellationToken);
        var result = new List<ChangeTransaction>(lines.Length);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var transaction = JsonSerializer.Deserialize<ChangeTransaction>(line, Options);
                if (transaction is not null)
                {
                    result.Add(transaction);
                }
            }
            catch (JsonException)
            {
                // Ignore a damaged line; valid transactions remain usable.
            }
        }

        return result;
    }

    private async Task WriteAllAsync(
        IReadOnlyList<ChangeTransaction> transactions,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var temp = $"{_filePath}.tmp-{Guid.NewGuid():N}";

        try
        {
            await using (var stream = File.Create(temp))
            await using (var writer = new StreamWriter(stream))
            {
                foreach (var transaction in transactions)
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(transaction, Options));
                }
            }

            File.Move(temp, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private sealed class TrackTagsJsonConverter : JsonConverter<TrackTags>
    {
        public override TrackTags Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected an object of tag values.");
            }

            var values = new List<KeyValuePair<TagField, string?>>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return new TrackTags(values);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                var name = reader.GetString() ?? string.Empty;
                reader.Read();
                var value = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();

                if (TagFieldCatalog.TryGetField(name, out var field))
                {
                    values.Add(new KeyValuePair<TagField, string?>(field, value));
                }
                else
                {
                    reader.Skip();
                }
            }

            throw new JsonException("Unexpected end of tag object.");
        }

        public override void Write(Utf8JsonWriter writer, TrackTags value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var (field, text) in value.Values)
            {
                writer.WriteString(TagFieldCatalog.Get(field).Token, text);
            }

            writer.WriteEndObject();
        }
    }
}
