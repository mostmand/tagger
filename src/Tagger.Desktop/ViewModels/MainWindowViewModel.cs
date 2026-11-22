using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Tagger.Application.Abstractions;
using Tagger.Application.Duplicates;
using Tagger.Application.Journal;
using Tagger.Application.Library;
using Tagger.Application.Settings;
using Tagger.Domain.Searching;
using Tagger.Domain.Tracks;
using Tagger.Desktop.Services;

namespace Tagger.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILibraryService _library;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _theme;
    private readonly IDialogService _dialogs;
    private readonly IUndoService _undo;
    private readonly IDuplicateFinder _duplicates;

    private readonly List<TrackRowViewModel> _allTracks = [];
    private readonly List<TrackRowViewModel> _selectedRows = [];
    private readonly List<string> _libraryRoots = [];
    private readonly Dictionary<string, bool> _folderExpansion = new(Application.Renaming.PathComparison.Comparer);
    private readonly LibraryTreeBuilder _treeBuilder = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Add a folder or drop files to start.";

    [ObservableProperty]
    private string _busyText = "Working…";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _isProgressIndeterminate;

    [ObservableProperty]
    private AppSettings _settings = AppSettings.Default;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private int _selectionCount;

    [ObservableProperty]
    private int _trackCount;

    [ObservableProperty]
    private string _libraryRoot = "No folder open";

    [ObservableProperty]
    private string _libraryTooltip = "No folder open";

    [ObservableProperty]
    private string _selectionSummary = "Nothing selected";

    [ObservableProperty]
    private string _collectionSummary = "0 tracks";

    public MainWindowViewModel(
        ILibraryService library,
        ISettingsService settingsService,
        IThemeService theme,
        IDialogService dialogs,
        IUndoService undo,
        IDuplicateFinder duplicates,
        TagEditorViewModel editor,
        PlayerViewModel player)
    {
        _library = library;
        _settingsService = settingsService;
        _theme = theme;
        _dialogs = dialogs;
        _undo = undo;
        _duplicates = duplicates;
        Editor = editor;
        Player = player;
        Editor.Saved += OnEditorSaved;
        Player.PreferencesChanged += OnPlayerPreferencesChanged;
        Player.CurrentTrackChanged += OnPlayerTrackChanged;
        Player.PlaybackFailed += (_, message) => StatusMessage = message;
        Editor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(TagEditorViewModel.HasChanges) or nameof(TagEditorViewModel.IsBusy))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        };
        VisibleTracks = new ObservableCollection<TrackRowViewModel>();
        VisibleTree = new ObservableCollection<LibraryNodeViewModel>();
    }

    public TagEditorViewModel Editor { get; }

    public ObservableCollection<TrackRowViewModel> VisibleTracks { get; }

    public ObservableCollection<LibraryNodeViewModel> VisibleTree { get; }

    public ObservableCollection<string> LibraryFolders { get; } = [];

    public IReadOnlyList<TrackRowViewModel> SelectedRows => _selectedRows;

    public event EventHandler? SelectionChanged;

    public event EventHandler? VisibleTracksRebuilt;

    public bool HasLibraryFolders => LibraryFolders.Count > 0;

    public bool HasTracks => _allTracks.Count > 0;

    public bool IsEmpty => !HasTracks && !IsBusy;

    public bool CanSave => HasSelection && Editor.HasChanges;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Settings = await _settingsService.LoadAsync(cancellationToken);
        _theme.Apply(Settings.Theme);
        Player.ApplyPreferences(Settings.PlaybackVolume, Settings.PlaybackMuted, Settings.PlaybackRepeat);

        if (!string.IsNullOrWhiteSpace(Settings.LastFolder) &&
            Directory.Exists(Settings.LastFolder))
        {
            await LoadPathsAsync([Settings.LastFolder], cancellationToken);
        }
        else
        {
            StatusMessage = "Add a folder or files to start organizing your library.";
        }
    }

    public void SetSelection(IEnumerable<TrackRowViewModel> rows)
    {
        ApplySelection(rows, notifyView: true);
    }

    /// <summary>Maps selected tree nodes (folders expand to all their tracks) into the editor selection.</summary>
    public void SelectTreeNodes(IEnumerable<LibraryNodeViewModel> nodes)
    {
        var rows = nodes.SelectMany(node => node switch
        {
            FolderNodeViewModel folder => folder.AllTracks,
            TrackRowViewModel track => [track],
            _ => []
        });

        ApplySelection(rows, notifyView: false);
    }

    public void ClearSelection()
    {
        ApplySelection([], notifyView: true);
    }

    private void ApplySelection(IEnumerable<TrackRowViewModel> rows, bool notifyView)
    {
        _selectedRows.Clear();
        _selectedRows.AddRange(rows.DistinctBy(x => x.Id));

        HasSelection = _selectedRows.Count > 0;
        SelectionCount = _selectedRows.Count;
        SelectionSummary = BuildSelectionSummary();
        Editor.LoadSelection(_selectedRows.Select(x => x.Track).ToList());
        OnPropertyChanged(nameof(CanSave));

        if (notifyView)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnEditorSaved(object? sender, Application.Editing.SaveResult result)
    {
        ApplyUpdatedTracks(result.UpdatedTracks);
        StatusMessage = $"Saved {result.UpdatedTracks.Count} track{(result.UpdatedTracks.Count == 1 ? string.Empty : "s")}.";
        OnPropertyChanged(nameof(CanSave));
    }

    private void RaiseCollectionState()
    {
        TrackCount = _allTracks.Count;
        CollectionSummary = BuildCollectionSummary();
        Player.ShowBar = _allTracks.Count > 0;
        OnPropertyChanged(nameof(HasTracks));
        OnPropertyChanged(nameof(CanPlay));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CanSave));
    }

    private string BuildCollectionSummary()
    {
        if (_allTracks.Count == 0)
        {
            return "0 tracks";
        }

        var duration = _allTracks
            .Select(x => x.Track.Audio?.Duration ?? TimeSpan.Zero)
            .Aggregate(TimeSpan.Zero, (sum, next) => sum + next);

        var formats = _allTracks.Select(x => x.Format).Distinct().Count();
        var trackLabel = _allTracks.Count == 1 ? "1 track" : $"{_allTracks.Count} tracks";
        return $"{trackLabel} · {duration.Hours + duration.Days * 24}h {duration.Minutes:00}m · {formats} format{(formats == 1 ? string.Empty : "s")}";
    }

    private string BuildSelectionSummary()
    {
        if (_selectedRows.Count == 0)
        {
            return "Nothing selected";
        }

        if (_selectedRows.Count == 1)
        {
            return _selectedRows[0].Title;
        }

        var artists = _selectedRows
            .Select(x => x.Artist)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return $"{_selectedRows.Count} tracks · {artists} artist{(artists == 1 ? string.Empty : "s")}";
    }
}
