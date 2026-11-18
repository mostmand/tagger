using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tagger.Application.Duplicates;
using Tagger.Application.Settings;
using Tagger.Domain.Searching;
using Tagger.Domain.Tracks;

namespace Tagger.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    [ObservableProperty]
    private TrackSortField _sortField = TrackSortField.FileName;

    [ObservableProperty]
    private bool _sortDescending;

    public string SortSummary => $"{SortFieldLabel(SortField)} {(SortDescending ? "↓" : "↑")}";

    private static string SortFieldLabel(TrackSortField field) =>
        field == TrackSortField.FileName ? "Name" : field.ToString();

    partial void OnSortFieldChanged(TrackSortField value) => OnPropertyChanged(nameof(SortSummary));

    partial void OnSortDescendingChanged(bool value) => OnPropertyChanged(nameof(SortSummary));

    [RelayCommand]
    private void ToggleSortDirection()
    {
        SortDescending = !SortDescending;
        RefreshVisible();
        StatusMessage = $"Sorted by {SortFieldLabel(SortField)} {(SortDescending ? "descending" : "ascending")}.";
    }

    [RelayCommand]
    private void ExpandAll() => SetAllFoldersExpanded(true);

    [RelayCommand]
    private void CollapseAll() => SetAllFoldersExpanded(false);

    private void SetAllFoldersExpanded(bool expanded)
    {
        foreach (var folder in EnumerateFolders(VisibleTree))
        {
            folder.IsExpanded = expanded;
        }

        StatusMessage = expanded ? "Expanded all folders." : "Collapsed all folders.";
    }

    [RelayCommand]
    private void Sort(string? field)
    {
        if (!Enum.TryParse<TrackSortField>(field, ignoreCase: true, out var parsed))
        {
            return;
        }

        if (parsed == SortField)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortField = parsed;
            SortDescending = false;
        }

        RefreshVisible();
        StatusMessage = $"Sorted by {parsed}{(SortDescending ? " (descending)" : string.Empty)}.";
    }

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        var files = await _dialogs.PickAudioFilesAsync();
        if (files is { Count: > 0 })
        {
            await LoadPathsAsync(files);
        }
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            await LoadPathsAsync([folder]);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await Editor.SaveAsync(Settings.AlwaysPreview);
        OnPropertyChanged(nameof(CanSave));
    }

    [RelayCommand]
    private void RemoveSelection()
    {
        if (_selectedRows.Count > 0)
        {
            RemoveRows(_selectedRows.ToList());
        }
    }

    [RelayCommand]
    private void ClearLibrary()
    {
        if (_allTracks.Count == 0)
        {
            return;
        }

        _allTracks.Clear();
        VisibleTracks.Clear();
        VisibleTree.Clear();
        _libraryRoots.Clear();
        _folderExpansion.Clear();
        LibraryFolders.Clear();
        UpdateLibraryRoot();
        OnPropertyChanged(nameof(HasLibraryFolders));
        ClearSelection();
        RaiseCollectionState();
        StatusMessage = "Library cleared. Files were not modified.";
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        if (_selectedRows.Count == 0)
        {
            return;
        }

        var updated = await _dialogs.ShowRenameAsync(
            _selectedRows.Select(x => x.Track).ToList(),
            Settings.RenamePattern);

        if (updated is { Count: > 0 })
        {
            ApplyUpdatedTracks(updated);
            StatusMessage = $"Renamed {updated.Count} file{(updated.Count == 1 ? string.Empty : "s")}.";
        }
    }

    [RelayCommand]
    private async Task ImportTagsAsync()
    {
        if (_selectedRows.Count == 0)
        {
            return;
        }

        var updated = await _dialogs.ShowImportTagsAsync(
            _selectedRows.Select(x => x.Track).ToList(),
            Settings.AutoTagPattern);

        if (updated is { Count: > 0 })
        {
            ApplyUpdatedTracks(updated);
            StatusMessage = $"Imported tags for {updated.Count} track{(updated.Count == 1 ? string.Empty : "s")}.";
        }
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var updated = await _dialogs.ShowSettingsAsync(Settings);
        if (updated is null)
        {
            return;
        }

        Settings = updated;
        _theme.Apply(Settings.Theme);
        await SaveSettingsAsync();
        StatusMessage = "Settings saved.";
    }

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        var next = Settings.Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        Settings = Settings with { Theme = next };
        _theme.Apply(next);
        await SaveSettingsAsync();
        StatusMessage = next == AppTheme.Dark ? "Dark theme" : "Light theme";
    }

    [RelayCommand]
    private async Task UndoAsync()
    {
        var result = await _undo.UndoLastAsync();
        StatusMessage = result.Message;

        if (!result.Success || result.Transaction is null)
        {
            return;
        }

        var remove = result.Transaction.TagWrites
            .Select(x => x.FilePath)
            .Concat(result.Transaction.FileMoves.Select(x => x.ToPath))
            .Distinct()
            .ToList();

        var reload = result.Transaction.TagWrites
            .Select(x => x.FilePath)
            .Concat(result.Transaction.FileMoves.Select(x => x.FromPath))
            .Distinct()
            .ToList();

        await ReloadPathsAsync(reload, remove);
    }

    [RelayCommand]
    private void FindDuplicates()
    {
        if (_allTracks.Count < 2)
        {
            return;
        }

        var groups = _duplicates.Find(_allTracks.Select(x => x.Track).ToList(), DuplicateMatchMode.ArtistAndTitle);
        if (groups.Count == 0)
        {
            StatusMessage = "No duplicate artist/title pairs found.";
            return;
        }

        var tracks = groups.SelectMany(x => x.Tracks).DistinctBy(x => x.Id).ToList();
        var rows = _allTracks.Where(x => tracks.Any(t => t.Id == x.Id)).ToList();
        SetSelection(rows);
        StatusMessage = $"Found {groups.Count} duplicate group{(groups.Count == 1 ? string.Empty : "s")} · {tracks.Count} tracks selected.";
    }
}
