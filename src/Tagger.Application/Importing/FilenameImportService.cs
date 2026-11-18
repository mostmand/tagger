using Tagger.Domain.Naming;
using Tagger.Domain.Tracks;

namespace Tagger.Application.Importing;

public sealed record FilenameImportEntry(
    Track Track,
    TrackTags ParsedTags,
    TagChangeSet Changes,
    string? Error = null)
{
    public bool IsValid => Error is null;

    public bool HasChanges => IsValid && !Changes.IsEmpty;
}

public sealed record FilenameImportPlan(IReadOnlyList<FilenameImportEntry> Entries)
{
    public int ErrorCount => Entries.Count(x => !x.IsValid);

    public int ChangeCount => Entries.Count(x => x.HasChanges);

    public bool HasChanges => ChangeCount > 0;
}

public interface IFilenameImportService
{
    FilenameImportPlan CreatePlan(IReadOnlyList<Track> tracks, TagPattern pattern);
}

/// <summary>Derives tag patches from filenames without writing anything.</summary>
public sealed class FilenameImportService : IFilenameImportService
{
    private readonly FilenamePatternParser _parser;

    public FilenameImportService(FilenamePatternParser parser)
    {
        _parser = parser;
    }

    public FilenameImportPlan CreatePlan(IReadOnlyList<Track> tracks, TagPattern pattern)
    {
        if (tracks.Count == 0)
        {
            return new FilenameImportPlan([]);
        }

        var entries = new List<FilenameImportEntry>(tracks.Count);
        foreach (var track in tracks)
        {
            var parsed = _parser.Parse(track.FileName, pattern);
            if (!parsed.Success)
            {
                entries.Add(new FilenameImportEntry(track, TrackTags.Empty, TagChangeSet.Empty, parsed.Error));
                continue;
            }

            var changes = new TagChangeSet();
            foreach (var (field, value) in parsed.Tags.Values)
            {
                if (!string.Equals(track.Tags.Get(field), value, StringComparison.Ordinal))
                {
                    changes.Add(field, value);
                }
            }

            entries.Add(new FilenameImportEntry(track, parsed.Tags, changes));
        }

        return new FilenameImportPlan(entries);
    }
}
