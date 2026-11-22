using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Tagger.Application.Renaming;
using Tagger.Domain.Naming;
using Tagger.Desktop.Services;
using Tagger.Domain.Tracks;

namespace Tagger.Desktop.ViewModels;

public sealed class RenameRowViewModel
{
    public RenameRowViewModel(RenamePlanEntry entry)
    {
        Entry = entry;
    }

    public RenamePlanEntry Entry { get; }

    public string OriginalName => Path.GetFileName(Entry.OriginalPath);

    public string TargetPath => Entry.TargetPath;

    public string TargetName => Entry.TargetFileName;

    public string TargetFolder => Entry.TargetDirectory;

    public bool IsValid => Entry.IsValid;

    public bool IsChanged => Entry.IsValid && !Entry.IsNoOp;

    public string Status => Entry.IsNoOp
        ? "Unchanged"
        : Entry.IsValid
            ? "Ready"
            : Entry.Error ?? "Invalid";

    public string Change => Entry.IsNoOp ? "No change" : Entry.TargetPath;
}

public sealed partial class RenameViewModel : ViewModelBase
{
    private readonly IRenameService _renameService;
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
    private string _statusMessage = "Adjust the pattern to preview new filenames.";

    public RenameViewModel(IRenameService renameService, IPlaybackMutationGuard mutationGuard)
    {
        _renameService = renameService;
        _mutationGuard = mutationGuard;
    }

    public ObservableCollection<RenameRowViewModel> Rows { get; } = [];

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
            var plan = BuildPlan();
            if (plan is null)
            {
                return false;
            }

            var paths = plan.Entries
                .Where(entry => entry.IsValid && !entry.IsNoOp)
                .Select(entry => entry.Track.FilePath)
                .ToList();

            var result = await _mutationGuard.RunAsync(
                paths,
                () => _renameService.ExecuteAsync(
                    plan,
                    $"Rename {plan.ChangeCount} file{(plan.ChangeCount == 1 ? string.Empty : "s")}",
                    cancellationToken),
                renameResult => renameResult.UpdatedTracks,
                cancellationToken);

            UpdatedTracks = result.UpdatedTracks;
            if (result.Failures.Count > 0)
            {
                ErrorMessage = result.Failures[0].Message;
                StatusMessage = $"Renamed {result.UpdatedTracks.Count}, {result.Failures.Count} failed.";
                return result.UpdatedTracks.Count > 0;
            }

            StatusMessage = $"Renamed {result.UpdatedTracks.Count} file{(result.UpdatedTracks.Count == 1 ? string.Empty : "s")}.";
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

        var plan = _renameService.CreatePlan(_tracks, pattern);
        foreach (var entry in plan.Entries)
        {
            Rows.Add(new RenameRowViewModel(entry));
        }

        ChangeCount = plan.ChangeCount;
        ErrorCount = plan.ErrorCount;
        StatusMessage = plan.HasChanges
            ? $"{plan.ChangeCount} file{(plan.ChangeCount == 1 ? string.Empty : "s")} will be renamed."
            : "Nothing will change.";
        OnPropertyChanged(nameof(CanApply));
    }

    private RenamePlan? BuildPlan()
    {
        if (!TagPattern.TryParse(Pattern, out var pattern, out var error) || pattern is null)
        {
            ErrorMessage = error;
            return null;
        }

        return _renameService.CreatePlan(_tracks, pattern);
    }
}
