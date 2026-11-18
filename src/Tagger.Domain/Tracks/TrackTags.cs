namespace Tagger.Domain.Tracks;

/// <summary>Immutable set of tag values. Empty and whitespace-only values are absent by definition.</summary>
public sealed class TrackTags : IEquatable<TrackTags>
{
    private readonly Dictionary<TagField, string> _values;

    public static TrackTags Empty { get; } = new([]);

    public TrackTags(IEnumerable<KeyValuePair<TagField, string?>> values)
    {
        _values = new Dictionary<TagField, string>();
        foreach (var (field, value) in values)
        {
            var normalized = Normalize(value);
            if (normalized is not null)
            {
                _values[field] = normalized;
            }
        }
    }

    public IReadOnlyDictionary<TagField, string> Values => _values;

    public int Count => _values.Count;

    public bool IsEmpty => _values.Count == 0;

    public string? Get(TagField field) => _values.GetValueOrDefault(field);

    public bool Contains(TagField field) => _values.ContainsKey(field);

    public TrackTags With(TagField field, string? value)
    {
        var normalized = Normalize(value);
        var copy = new Dictionary<TagField, string>(_values);
        if (normalized is null)
        {
            copy.Remove(field);
        }
        else
        {
            copy[field] = normalized;
        }

        return new TrackTags(copy.Select(x => new KeyValuePair<TagField, string?>(x.Key, x.Value)));
    }

    public TrackTags WithMany(IEnumerable<TagChange> changes)
    {
        var copy = new Dictionary<TagField, string>(_values);
        foreach (var change in changes)
        {
            var normalized = Normalize(change.Value);
            if (normalized is null)
            {
                copy.Remove(change.Field);
            }
            else
            {
                copy[change.Field] = normalized;
            }
        }

        return new TrackTags(copy.Select(x => new KeyValuePair<TagField, string?>(x.Key, x.Value)));
    }

    public TrackTags Without(TagField field) => With(field, null);

    public bool Equals(TrackTags? other)
    {
        if (other is null || other.Count != Count)
        {
            return false;
        }

        return _values.All(pair => other.Get(pair.Key) == pair.Value);
    }

    public override bool Equals(object? obj) => obj is TrackTags other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var pair in _values.OrderBy(x => x.Key))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
