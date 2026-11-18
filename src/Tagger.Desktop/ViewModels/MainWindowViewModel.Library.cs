using Tagger.Application.Library;
using Tagger.Application.Renaming;
using Tagger.Domain.Searching;
using Tagger.Domain.Tracks;

namespace Tagger.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public async Task LoadPathsAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken = default)
    {
        if (paths.Count == 0 || IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsProgressIndeterminate = true;
        BusyText = "Scanning audio files…";
        ProgressValue = 0;
        OnPropertyChanged(nameof(IsEmpty));

        try
        {
            var progress = new Progress<LibraryProgress>(value =>
            {
                if (value.Total > 0)
                {
                    ProgressValue = value.Loaded * 100d / value.Total;
                    BusyText = $"Reading {value.Loaded + 1} of {value.Total} · {Path.GetFileName(value.CurrentFile)}";
                    IsProgressIndeterminate = false;
                }
            });

            var result = await _library.LoadAsync(paths, Settings.Recursive, progress, cancellationToken);
            MergeTracks(result.Tracks);
            UpdateLibraryRoot(paths);
            RefreshVisible();

            StatusMessage = result.Failures.Count == 0
                ? $"Loaded {result.Tracks.Count} track{(result.Tracks.Count == 1 ? string.Empty : "s")}."
                : $"Loaded {result.Tracks.Count} track{(result.Tracks.Count == 1 ? string.Empty : "s")} · {result.Failures.Count} could not be read";

            await SaveSettingsAsync(cancellationToken);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(CanSave));
        }
    }

    public async Task ReloadPathsAsync(
        IReadOnlyList<string> paths,
        IReadOnlyList<string> pathsToRemove,
        CancellationToken cancellationToken = default)
    {
        foreach (var path in pathsToRemove)
        {
            var row = _allTracks.FirstOrDefault(x => PathComparisonEquals(x.FilePath, path));
            if (row is not null)
            {
                _allTracks.Remove(row);
            }
        }

        if (paths.Count > 0)
        {
            var result = await _library.LoadAsync(paths, recursive: false, cancellationToken: cancellationToken);
            MergeTracks(result.Tracks);
        }

        RefreshVisible();
        RaiseCollectionState();
    }

    private void MergeTracks(IReadOnlyList<Track> tracks)
    {
        foreach (var track in tracks)
        {
            var existing = _allTracks.FirstOrDefault(x => PathComparisonEquals(x.FilePath, track.FilePath));
            if (existing is not null)
            {
                existing.Update(track);
            }
            else
            {
                _allTracks.Add(new TrackRowViewModel(track, _allTracks.Count + 1));
            }
        }

        RenumberRows();
        RaiseCollectionState();
    }

    private void ApplyUpdatedTracks(IReadOnlyList<Track> tracks)
    {
        foreach (var track in tracks)
        {
            var row = _allTracks.FirstOrDefault(x => x.Id == track.Id);
            if (row is null)
            {
                continue;
            }

            row.Update(track);
        }

        RenumberRows();
        RefreshVisible();
        RaiseCollectionState();
    }

    public void RefreshVisible()
    {
        var query = TrackSearchQuery.Parse(SearchText);
        var matches = _allTracks
            .Where(x => query.Matches(x.Track))
            .ToList();

        var byId = matches.ToDictionary(x => x.Id);
        matches = TrackSorter
            .Sort(matches.Select(x => x.Track), SortField, SortDescending)
            .Select(x => byId[x.Id])
            .ToList();

        VisibleTracks.Clear();
        foreach (var row in matches)
        {
            VisibleTracks.Add(row);
        }

        for (var i = 0; i < VisibleTracks.Count; i++)
        {
            VisibleTracks[i].Index = i + 1;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            StatusMessage = $"{matches.Count} of {_allTracks.Count} tracks match “{SearchText}”.";
        }
    }

    private void RemoveRows(IEnumerable<TrackRowViewModel> rows)
    {
        foreach (var row in rows.ToList())
        {
            _allTracks.Remove(row);
            VisibleTracks.Remove(row);
        }

        RenumberRows();
        RaiseCollectionState();
        ClearSelection();
    }

    private void RenumberRows()
    {
        for (var i = 0; i < _allTracks.Count; i++)
        {
            _allTracks[i].Index = i + 1;
        }
    }

    private void UpdateLibraryRoot(IReadOnlyList<string> paths)
    {
        var folder = paths.Count == 1 && Directory.Exists(paths[0])
            ? paths[0]
            : Path.GetDirectoryName(paths[0]);

        if (folder is not null)
        {
            LibraryRoot = folder;
            Settings = Settings with { LastFolder = folder };
        }
    }

    private async Task SaveSettingsAsync(CancellationToken cancellationToken = default)
    {
        await _settingsService.SaveAsync(Settings, cancellationToken);
    }

    partial void OnSearchTextChanged(string value) => RefreshVisible();

    private static bool PathComparisonEquals(string left, string right) =>
        PathComparison.Equals(left, right);
}
