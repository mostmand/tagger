namespace Tagger.Desktop.ViewModels;

/// <summary>A folder group in the library tree, with its subfolders and tracks.</summary>
public sealed class FolderNodeViewModel : LibraryNodeViewModel
{
    public FolderNodeViewModel(
        string path,
        string name,
        IReadOnlyList<LibraryNodeViewModel> children,
        IReadOnlyList<TrackRowViewModel> tracks)
    {
        Path = path;
        Name = name;
        Children = children;
        Tracks = tracks;
        AllTracks = children
            .SelectMany(child => child switch
            {
                FolderNodeViewModel folder => folder.AllTracks,
                TrackRowViewModel track => [track],
                _ => []
            })
            .ToList();
        TotalDuration = AllTracks
            .Select(x => x.Track.Audio?.Duration ?? TimeSpan.Zero)
            .Aggregate(TimeSpan.Zero, (sum, next) => sum + next);
    }

    public string Path { get; }

    public string Name { get; }

    public IReadOnlyList<LibraryNodeViewModel> Children { get; }

    /// <summary>Tracks directly inside this folder, in the active sort order.</summary>
    public IReadOnlyList<TrackRowViewModel> Tracks { get; }

    /// <summary>Every track in this folder and its subfolders, in tree order.</summary>
    public IReadOnlyList<TrackRowViewModel> AllTracks { get; }

    public TimeSpan TotalDuration { get; }

    public int TrackCount => AllTracks.Count;

    public string TrackCountText => TrackCount == 1 ? "1 track" : $"{TrackCount} tracks";

    public string DurationText =>
        TotalDuration == TimeSpan.Zero
            ? string.Empty
            : $"{(int)TotalDuration.TotalHours}h {TotalDuration.Minutes:00}m";

    public override string ToString() => Name;
}
