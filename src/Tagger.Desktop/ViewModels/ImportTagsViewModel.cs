using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Tagger.Application.Editing;
using Tagger.Application.Importing;
using Tagger.Domain.Naming;
using Tagger.Desktop.Services;
using Tagger.Domain.Tracks;

namespace Tagger.Desktop.ViewModels;

public sealed class ImportRowViewModel
{
    public ImportRowViewModel(FilenameImportEntry entry)
    {
        Entry = entry;
    }

    public FilenameImportEntry Entry { get; }

    public string FileName => Entry.Track.FileName;

    public string CurrentTitle => Entry.Track.DisplayTitle;

    public string ParsedPreview => Entry.IsValid
        ? string.Join(" · ", Entry.ParsedTags.Values
            .Take(4)
            .Select(x => $"{TagFieldCatalog.Get(x.Key).DisplayName}: {x.Value}"))
        : Entry.Error ?? "Could not parse filename";

    public bool IsValid => Entry.IsValid;

    public bool HasChanges => Entry.HasChanges;

    public string Status => !Entry.IsValid ? "Error" : Entry.HasChanges ? (Entry.Changes.Count == 1 ? "1 field" : $"{Entry.Changes.Count} fields") : "No change";
}

public sealed partial class ImportTagsViewModel : ViewModelBase
{
    private readonly IFilenameImportService _importService;
    private readonly ITagSaveService _saveService;
    private readonly IPlaybackMutationGuard _mutationGuard;
    private IReadOnlyList<Track> _tracks = [];
    private bool _initializing;

    [ObservableProperty]
    private string _pattern = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _changeCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private string _statusMessage = "Preview the tags that will be read from filenames.";

    public ImportTagsViewModel(
        IFilenameImportService importService,
        ITagSaveService saveService,
        IPlaybackMutationGuard mutationGuard)
    {
        _importService = importService;
        _saveService = saveService;
        _mutationGuard = mutationGuard;
    }

    public ObservableCollection<ImportRowViewModel> Rows { get; } = [];

    public IReadOnlyList<Track> UpdatedTracks { get; private set; } = [];

    public bool CanApply => ChangeCount > 0 && !IsBusy;

    public void Initialize(IReadOnlyList<Track> tracks, string defaultPattern)
    {
        _initializing = true;
        _tracks = tracks;
        Pattern = defaultPattern;
        _initializing = false;
        Refresh();
    }

    public async Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (!CanApply)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            var entries = Rows
                .Where(x => x.Entry.IsValid && x.Entry.HasChanges)
                .Select(x => x.Entry)
                .ToList();

            var requests = entries
                .Select(x => new TagEditRequest(x.Track, x.Changes))
                .ToList();

            var result = await _mutationGuard.RunAsync(
                requests.Select(request => request.Track.FilePath).ToList(),
                () => _saveService.ApplyAsync(
                    requests,
                    $"Import tags for {requests.Count} track{(requests.Count == 1 ? string.Empty : "s")}",
                    cancellationToken),
                saveResult => saveResult.UpdatedTracks,
                cancellationToken);

            UpdatedTracks = result.UpdatedTracks;

            if (result.Failures.Count > 0)
            {
                ErrorMessage = result.Failures[0].Message;
                StatusMessage = $"Imported {result.UpdatedTracks.Count}, {result.Failures.Count} failed.";
                return result.UpdatedTracks.Count > 0;
            }

            StatusMessage = $"Imported tags for {result.UpdatedTracks.Count} track{(result.UpdatedTracks.Count == 1 ? string.Empty : "s")}.";
            return true;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanApply));
        }
    }

    partial void OnPatternChanged(string value)
    {
        if (!_initializing)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        ErrorMessage = null;
        Rows.Clear();

        if (!TagPattern.TryParse(Pattern, out var pattern, out var error) || pattern is null)
        {
            ErrorMessage = error;
            ChangeCount = 0;
            ErrorCount = 0;
            StatusMessage = "Pattern is invalid.";
            OnPropertyChanged(nameof(CanApply));
            return;
        }

        var plan = _importService.CreatePlan(_tracks, pattern);
        foreach (var entry in plan.Entries)
        {
            Rows.Add(new ImportRowViewModel(entry));
        }

        ChangeCount = plan.ChangeCount;
        ErrorCount = plan.ErrorCount;
        StatusMessage = plan.HasChanges
            ? $"{plan.ChangeCount} track{(plan.ChangeCount == 1 ? string.Empty : "s")} will receive tags."
            : "No tags would change.";
        OnPropertyChanged(nameof(CanApply));
    }
}
