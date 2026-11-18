namespace Tagger.Domain.Tracks;

/// <summary>A single field mutation. A null value clears the field.</summary>
public sealed record TagChange(TagField Field, string? Value);
