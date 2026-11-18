namespace Tagger.Domain.Tracks;

/// <summary>An editable set of field patches. Only fields present here are written.</summary>
public sealed class TagChangeSet
{
    private readonly Dictionary<TagField, string?> _changes = [];

    public static TagChangeSet Empty => new();

    public IReadOnlyDictionary<TagField, string?> Values => _changes;

    public int Count => _changes.Count;

    public bool IsEmpty => _changes.Count == 0;

    public bool Contains(TagField field) => _changes.ContainsKey(field);

    public string? Get(TagField field) => _changes.GetValueOrDefault(field);

    public TagChangeSet Add(TagField field, string? value)
    {
        _changes[field] = value;
        return this;
    }

    public TagChangeSet Add(TagChange change) => Add(change.Field, change.Value);

    public TagChangeSet AddRange(IEnumerable<TagChange> changes)
    {
        foreach (var change in changes)
        {
            Add(change);
        }

        return this;
    }

    public TagChangeSet Remove(TagField field)
    {
        _changes.Remove(field);
        return this;
    }

    public IEnumerable<TagChange> ToChanges() => _changes.Select(x => new TagChange(x.Key, x.Value));

    public TagChangeSet Clone() => new TagChangeSet().AddRange(ToChanges());

    public static TagChangeSet From(IEnumerable<TagChange> changes) => new TagChangeSet().AddRange(changes);

    public static TagChangeSet From(IEnumerable<KeyValuePair<TagField, string?>> changes) =>
        new TagChangeSet().AddRange(changes.Select(x => new TagChange(x.Key, x.Value)));
}
