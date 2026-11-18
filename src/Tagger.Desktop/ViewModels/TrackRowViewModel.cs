using CommunityToolkit.Mvvm.ComponentModel;
using Tagger.Domain.Tracks;

namespace Tagger.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject;

public sealed class TrackRowViewModel : ViewModelBase
{
    private Track _track;
    private int _index;

    public TrackRowViewModel(Track track, int index)
    {
        _track = track;
        _index = index;
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

    public string Folder => _track.DirectoryPath;

    public bool HasArtwork => _track.CoverArt is not null;

    public void Update(Track track)
    {
        _track = track;
        OnPropertyChanged(string.Empty);
    }
}
