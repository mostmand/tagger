using System.Collections.ObjectModel;
using Tagger.Domain.Tracks;

namespace Tagger.Desktop.ViewModels;

public sealed class ChangePreviewRowViewModel
{
    public ChangePreviewRowViewModel(string fileName, string field, string? before, string? after)
    {
        FileName = fileName;
        Field = field;
        Before = before ?? string.Empty;
        After = after ?? string.Empty;
    }

    public string FileName { get; }

    public string Field { get; }

    public string Before { get; }

    public string After { get; }
}

public sealed class ChangePreviewViewModel
{
    public ChangePreviewViewModel(ChangePreview preview, string title)
    {
        Title = title;
        Rows = new ObservableCollection<ChangePreviewRowViewModel>();

        foreach (var track in preview.Tracks.Where(x => x.HasChanges))
        {
            foreach (var diff in track.Diffs)
            {
                Rows.Add(new ChangePreviewRowViewModel(
                    track.Track.FileName,
                    TagFieldCatalog.Get(diff.Field).DisplayName,
                    diff.Before,
                    diff.After));
            }

            if (track.CoverArtChanged)
            {
                Rows.Add(new ChangePreviewRowViewModel(
                    track.Track.FileName,
                    "Artwork",
                    track.Track.CoverArt is null ? "No artwork" : "Existing artwork",
                    "Changed"));
            }
        }

        Summary = $"{preview.ChangedTrackCount} track{(preview.ChangedTrackCount == 1 ? string.Empty : "s")} · {preview.ChangedFieldCount} field{(preview.ChangedFieldCount == 1 ? string.Empty : "s")}";
    }

    public string Title { get; }

    public string Summary { get; }

    public ObservableCollection<ChangePreviewRowViewModel> Rows { get; }
}
