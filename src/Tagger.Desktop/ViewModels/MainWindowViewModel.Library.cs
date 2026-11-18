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
            RegisterLibraryRoots(paths);
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

        ApplyFolderLabels();
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

        CaptureExpansionStates();
        var tree = _treeBuilder.Build(
            matches,
            _libraryRoots,
            SortField,
            SortDescending,
            _folderExpansion);

        VisibleTree.Clear();
        foreach (var node in tree)
        {
            VisibleTree.Add(node);
        }

        VisibleTracks.Clear();
        foreach (var row in tree.SelectMany(x => x.AllTracks))
        {
            VisibleTracks.Add(row);
        }

        for (var i = 0; i < VisibleTracks.Count; i++)
        {
            VisibleTracks[i].Index = i + 1;
        }

        PruneSelectionToVisible();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            StatusMessage = $"{VisibleTracks.Count} of {_allTracks.Count} tracks match “{SearchText}”.";
        }

        VisibleTracksRebuilt?.Invoke(this, EventArgs.Empty);
    }

    private void CaptureExpansionStates()
    {
        foreach (var folder in EnumerateFolders(VisibleTree))
        {
            _folderExpansion[folder.Path] = folder.IsExpanded;
        }
    }

    private static IEnumerable<FolderNodeViewModel> EnumerateFolders(
        IEnumerable<LibraryNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is not FolderNodeViewModel folder)
            {
                continue;
            }

            yield return folder;
            foreach (var child in EnumerateFolders(folder.Children))
            {
                yield return child;
            }
        }
    }

    private void PruneSelectionToVisible()
    {
        var visible = VisibleTracks.Select(x => x.Id).ToHashSet();
        var kept = _selectedRows.Where(x => visible.Contains(x.Id)).ToList();

        if (kept.Count != _selectedRows.Count)
        {
            ApplySelection(kept, notifyView: false);
        }
    }

    private void RemoveRows(IEnumerable<TrackRowViewModel> rows)
    {
        foreach (var row in rows.ToList())
        {
            _allTracks.Remove(row);
        }

        PruneLibraryRoots();
        RenumberRows();
        RaiseCollectionState();
        ClearSelection();
        RefreshVisible();
    }

    private void PruneLibraryRoots()
    {
        var removed = _libraryRoots.RemoveAll(root =>
            !_allTracks.Any(row => IsUnderRoot(row.Folder, root)));

        if (removed == 0)
        {
            return;
        }

        LibraryFolders.Clear();
        foreach (var root in _libraryRoots)
        {
            LibraryFolders.Add(root);
        }

        OnPropertyChanged(nameof(HasLibraryFolders));
        UpdateLibraryRoot();
        ApplyFolderLabels();
    }

    private static bool IsUnderRoot(string directory, string root) =>
        PathComparisonEquals(directory, root) ||
        directory.StartsWith(root + Path.DirectorySeparatorChar, PathComparison.Comparison);

    private void RenumberRows()
    {
        for (var i = 0; i < _allTracks.Count; i++)
        {
            _allTracks[i].Index = i + 1;
        }
    }

    private void RegisterLibraryRoots(IReadOnlyList<string> paths)
    {
        var added = false;

        foreach (var path in paths)
        {
            var root = ResolveLibraryRoot(path);
            if (string.IsNullOrWhiteSpace(root) || _libraryRoots.Any(x => PathComparisonEquals(x, root)))
            {
                continue;
            }

            _libraryRoots.Add(root);
            LibraryFolders.Add(root);
            added = true;
        }

        if (added)
        {
            OnPropertyChanged(nameof(HasLibraryFolders));
        }

        UpdateLibraryRoot();
        ApplyFolderLabels();
    }

    private void UpdateLibraryRoot()
    {
        LibraryRoot = _libraryRoots.Count switch
        {
            0 => "No folder open",
            1 => _libraryRoots[0],
            _ => $"{_libraryRoots.Count} folders"
        };

        LibraryTooltip = _libraryRoots.Count == 0
            ? "No folder open"
            : string.Join(Environment.NewLine, _libraryRoots);

        if (_libraryRoots.Count > 0)
        {
            Settings = Settings with { LastFolder = _libraryRoots[^1] };
        }
    }

    private void ApplyFolderLabels()
    {
        var formatter = new FolderLabelFormatter(_libraryRoots);
        foreach (var row in _allTracks)
        {
            row.UpdateFolderDisplay(formatter.Format(row.Folder));
        }
    }

    private static string? ResolveLibraryRoot(string path)
    {
        if (Directory.Exists(path))
        {
            return path;
        }

        if (File.Exists(path))
        {
            return Path.GetDirectoryName(path);
        }

        // Paths that do not exist yet are classified by shape: files have an extension.
        return Path.HasExtension(path) ? Path.GetDirectoryName(path) : path;
    }

    private async Task SaveSettingsAsync(CancellationToken cancellationToken = default)
    {
        await _settingsService.SaveAsync(Settings, cancellationToken);
    }

    partial void OnSearchTextChanged(string value) => RefreshVisible();

    private static bool PathComparisonEquals(string left, string right) =>
        PathComparison.Equals(left, right);
}
