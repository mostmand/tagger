using CommunityToolkit.Mvvm.ComponentModel;
using Tagger.Domain.Tracks;

namespace Tagger.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject;

public sealed class TrackRowViewModel : LibraryNodeViewModel
{
    private Track _track;
    private int _index;
    private string _folderDisplay;

    public TrackRowViewModel(Track track, int index)
    {
        _track = track;
        _index = index;
        _folderDisplay = GetFolderName(track.DirectoryPath);
    }

    public Track Track => _track;

    public Guid Id => _track.Id;

    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    public string Title => _track.DisplayTitle;

    public string Artist => _track.DisplayArtist;

    public string Album => _track.DisplayAlbum;

    public string AlbumArtist => _track.Tags.Get(TagField.AlbumArtist) ?? string.Empty;

    public string Year => _track.Tags.Get(TagField.Year) ?? string.Empty;

    public string Genre => _track.Tags.Get(TagField.Genre) ?? string.Empty;

    public string TrackNumber => _track.Tags.Get(TagField.Track) ?? string.Empty;

    public string Duration => _track.DurationText;

    public string Format => _track.Format;

    public string Bitrate => _track.Audio?.BitrateText ?? string.Empty;

    public string FileName => _track.FileName;

    public string FilePath => _track.FilePath;

    public string DetailLine
    {
        get
        {
            var parts = new List<string>(5);
            if (!string.IsNullOrWhiteSpace(Artist))
            {
                parts.Add(Artist);
            }

            if (!string.IsNullOrWhiteSpace(Album))
            {
                parts.Add(Album);
            }

            if (!string.IsNullOrWhiteSpace(Year))
            {
                parts.Add(Year);
            }

            if (!string.IsNullOrWhiteSpace(Genre))
            {
                parts.Add(Genre);
            }

            parts.Add(FileName);
            return string.Join(" · ", parts);
        }
    }

    public string Folder => _track.DirectoryPath;

    /// <summary>Short folder label relative to the loaded library roots.</summary>
    public string FolderDisplay
    {
        get => _folderDisplay;
        private set => SetProperty(ref _folderDisplay, value);
    }

    public bool HasArtwork => _track.CoverArt is not null;

    public void Update(Track track)
    {
        _track = track;
        _folderDisplay = GetFolderName(track.DirectoryPath);
        OnPropertyChanged(string.Empty);
    }

    public void UpdateFolderDisplay(string display)
    {
        FolderDisplay = display;
    }

    private static string GetFolderName(string directoryPath)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(directoryPath);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? directoryPath : name;
    }

    public override string ToString() => Title;
}
