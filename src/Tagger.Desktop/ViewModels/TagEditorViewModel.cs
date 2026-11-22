using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Tagger.Application.Abstractions;
using Tagger.Application.Artwork;
using Tagger.Application.Editing;
using Tagger.Application.Library;
using Tagger.Domain.Tracks;
using Tagger.Desktop.Services;

namespace Tagger.Desktop.ViewModels;

/// <summary>Edits a selection with mixed-value awareness and previews every write.</summary>
public sealed partial class TagEditorViewModel : ViewModelBase
{
    private readonly ISelectionSummaryService _summaryService;
    private readonly ITagSaveService _saveService;
    private readonly IChangePreviewService _previewService;
    private readonly ICoverArtService _coverArtService;
    private readonly IDialogService _dialogs;
    private readonly IClock _clock;
    private readonly IPlaybackMutationGuard _mutationGuard;

    private IReadOnlyList<Track> _tracks = [];
    private CoverArtUpdate _artworkUpdate = CoverArtUpdate.Keep;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private string _statusMessage = "Select a track to edit its tags.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private byte[]? _artworkBytes;

    [ObservableProperty]
    private string? _artworkMimeType;

    [ObservableProperty]
    private bool _isArtworkMixed;

    [ObservableProperty]
    private string _artworkSummary = "No artwork";

    public TagEditorViewModel(
        ISelectionSummaryService summaryService,
        ITagSaveService saveService,
        IChangePreviewService previewService,
        ICoverArtService coverArtService,
        IDialogService dialogs,
        IClock clock,
        IPlaybackMutationGuard mutationGuard)
    {
        _summaryService = summaryService;
        _saveService = saveService;
        _previewService = previewService;
        _coverArtService = coverArtService;
        _dialogs = dialogs;
        _clock = clock;
        _mutationGuard = mutationGuard;

        var year = clock.UtcNow.Year;
        AllFields = TagFieldCatalog.All
            .Select(info => new TagFieldEditorViewModel(info, year))
            .ToList();

        foreach (var field in AllFields)
        {
            field.PropertyChanged += OnFieldChanged;
        }

        BasicFields = new ObservableCollection<TagFieldEditorViewModel>(
            AllFields.Where(x => x.Info.Group == TagFieldGroup.Basic));
        ReleaseFields = new ObservableCollection<TagFieldEditorViewModel>(
            AllFields.Where(x => x.Info.Group == TagFieldGroup.Release));
        CreditsFields = new ObservableCollection<TagFieldEditorViewModel>(
            AllFields.Where(x => x.Info.Group == TagFieldGroup.Credits));
        AdvancedFields = new ObservableCollection<TagFieldEditorViewModel>(
            AllFields.Where(x => x.Info.Group == TagFieldGroup.Advanced));
    }

    public ArtworkViewerViewModel ArtworkViewer { get; } = new();

    public IReadOnlyList<TagFieldEditorViewModel> AllFields { get; }

    public ObservableCollection<TagFieldEditorViewModel> BasicFields { get; }

    public ObservableCollection<TagFieldEditorViewModel> ReleaseFields { get; }

    public ObservableCollection<TagFieldEditorViewModel> CreditsFields { get; }

    public ObservableCollection<TagFieldEditorViewModel> AdvancedFields { get; }

    public IReadOnlyList<Track> Tracks => _tracks;

    public event EventHandler<SaveResult>? Saved;

    public void LoadSelection(IReadOnlyList<Track> tracks)
    {
        _tracks = tracks;
        _artworkUpdate = CoverArtUpdate.Keep;
        SelectedCount = tracks.Count;
        HasSelection = tracks.Count > 0;

        if (tracks.Count == 0)
        {
            foreach (var field in AllFields)
            {
                field.Load(new TagFieldSummary(field.Field, null, false, 0));
            }

            SetArtwork(null, "No artwork");
            ArtworkMimeType = null;
            IsArtworkMixed = false;
            ArtworkViewer.Close();
            HasChanges = false;
            StatusMessage = "Select a track to edit its tags.";
            return;
        }

        var summary = _summaryService.Summarize(tracks);
        foreach (var field in AllFields)
        {
            field.Load(summary.For(field.Field));
        }

        ArtworkMimeType = summary.CommonCoverArt?.MimeType;
        IsArtworkMixed = summary.IsCoverArtMixed;
        var artworkSummary = summary.IsCoverArtMixed
            ? "Mixed artwork"
            : summary.CommonCoverArt is null
                ? "No artwork"
                : $"{summary.CommonCoverArt.MimeType} · {FormatSize(summary.CommonCoverArt.SizeInBytes)}";
        SetArtwork(summary.CommonCoverArt?.Data, artworkSummary);

        if (ArtworkBytes is null)
        {
            ArtworkViewer.Close();
        }

        HasChanges = false;
        StatusMessage = tracks.Count == 1
            ? "Editing 1 track"
            : $"Editing {tracks.Count} tracks · mixed values are preserved until edited";
    }

    public async Task SaveAsync(bool previewRequired, CancellationToken cancellationToken = default)
    {
        if (_tracks.Count == 0 || IsBusy)
        {
            return;
        }

        var invalid = AllFields.FirstOrDefault(x => x.Error is not null);
        if (invalid is not null)
        {
            StatusMessage = $"{invalid.Label}: {invalid.Error}";
            return;
        }

        var changes = new TagChangeSet();
        foreach (var field in AllFields.Where(x => x.IsDirty))
        {
            changes.Add(field.Field, field.Value);
        }

        if (changes.IsEmpty && !_artworkUpdate.ShouldUpdate)
        {
            StatusMessage = "No changes to save.";
            return;
        }

        var preview = _previewService.Create(_tracks, changes, _artworkUpdate);
        if (!preview.HasChanges)
        {
            StatusMessage = "No changes to save.";
            return;
        }

        if (previewRequired &&
            !await _dialogs.ShowChangePreviewAsync(preview, "Preview changes"))
        {
            StatusMessage = "Save cancelled.";
            return;
        }

        IsBusy = true;
        try
        {
            var requests = _tracks
                .Select(track => new TagEditRequest(track, changes, _artworkUpdate))
                .ToList();

            var result = await _mutationGuard.RunAsync(
                requests.Select(request => request.Track.FilePath).ToList(),
                () => _saveService.ApplyAsync(requests, BuildSummary(preview), cancellationToken),
                saveResult => saveResult.UpdatedTracks,
                cancellationToken);

            if (result.UpdatedTracks.Count > 0)
            {
                LoadSelection(result.UpdatedTracks);
                Saved?.Invoke(this, result);
            }

            StatusMessage = result.Failures.Count == 0
                ? $"Saved {result.UpdatedTracks.Count} track{(result.UpdatedTracks.Count == 1 ? string.Empty : "s")}."
                : $"Saved with {result.Failures.Count} error(s): {result.Failures[0].Message}";

            if (result.Failures.Count > 0)
            {
                await _dialogs.ShowErrorAsync("Some files could not be saved", result.Failures[0].Message);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ChooseArtworkAsync(CancellationToken cancellationToken = default)
    {
        var data = await _dialogs.PickArtworkAsync(cancellationToken);
        if (data is null)
        {
            return;
        }

        var result = _coverArtService.Create(data);
        if (!result.Success || result.Artwork is null)
        {
            await _dialogs.ShowErrorAsync("Artwork not accepted", result.Error ?? "Unknown image error.");
            return;
        }

        _artworkUpdate = CoverArtUpdate.Set(result.Artwork);
        SetArtwork(result.Artwork.Data, $"{result.Artwork.MimeType} · {FormatSize(result.Artwork.SizeInBytes)}");
        ArtworkMimeType = result.Artwork.MimeType;
        IsArtworkMixed = false;
        HasChanges = true;
        StatusMessage = "Artwork will be replaced when you save.";
    }

    public void RemoveArtwork()
    {
        if (SelectedCount == 0)
        {
            return;
        }

        _artworkUpdate = CoverArtUpdate.Remove;
        SetArtwork(null, "Artwork will be removed");
        ArtworkMimeType = null;
        IsArtworkMixed = false;
        ArtworkViewer.Close();
        HasChanges = true;
        StatusMessage = "Artwork will be removed when you save.";
    }

    public void Reset()
    {
        LoadSelection(_tracks);
    }

    private void SetArtwork(byte[]? bytes, string summary)
    {
        ArtworkBytes = bytes;
        ArtworkSummary = summary;
        ArtworkViewer.ArtworkBytes = bytes;
        ArtworkViewer.ArtworkSummary = summary;
    }

    private void OnFieldChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TagFieldEditorViewModel.IsDirty) or nameof(TagFieldEditorViewModel.IsMixed))
        {
            HasChanges = AllFields.Any(x => x.IsDirty) || _artworkUpdate.ShouldUpdate;
        }
    }

    private static string BuildSummary(ChangePreview preview) =>
        $"Edit tags · {preview.ChangedTrackCount} track{(preview.ChangedTrackCount == 1 ? string.Empty : "s")} · {preview.ChangedFieldCount} field{(preview.ChangedFieldCount == 1 ? string.Empty : "s")}";

    private static string FormatSize(int bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:0.0} MB"
            : $"{bytes / 1024d:0} KB";

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task PickArtworkAsync()
    {
        await ChooseArtworkAsync();
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ClearArtwork() => RemoveArtwork();

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ResetEdits() => Reset();

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void OpenArtwork()
    {
        if (ArtworkBytes is null)
        {
            return;
        }

        ArtworkViewer.Open();
    }
}
