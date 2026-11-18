using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using Tagger.Application.Settings;
using Tagger.Domain.Tracks;
using Tagger.Desktop.Views;

namespace Tagger.Desktop.Services;

public sealed class DialogService : IDialogService
{
    private static readonly FilePickerFileType AudioFiles = new("Audio files")
    {
        Patterns =
        [
            "*.mp3", "*.flac", "*.ogg", "*.opus", "*.m4a", "*.m4b", "*.mp4",
            "*.wav", "*.aiff", "*.aif", "*.ape", "*.wv", "*.mpc", "*.wma", "*.dsf", "*.dff"
        ]
    };

    private static readonly FilePickerFileType ImageFiles = new("Images")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp"]
    };

    private readonly IServiceProvider _services;
    private readonly IWindowProvider _windows;

    public DialogService(IServiceProvider services, IWindowProvider windows)
    {
        _services = services;
        _windows = windows;
    }

    public async Task<IReadOnlyList<string>?> PickAudioFilesAsync(CancellationToken cancellationToken = default)
    {
        var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add audio files",
            AllowMultiple = true,
            FileTypeFilter = [AudioFiles]
        });

        return files
            .Select(x => x.TryGetLocalPath())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();
    }

    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        var folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add a music folder",
            AllowMultiple = false
        });

        return folders.Select(x => x.TryGetLocalPath()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    public async Task<byte[]?> PickArtworkAsync(CancellationToken cancellationToken = default)
    {
        var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose cover artwork",
            AllowMultiple = false,
            FileTypeFilter = [ImageFiles]
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        await using var stream = await file.OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    public Task<bool> ShowChangePreviewAsync(ChangePreview preview, string title)
    {
        var viewModel = new ViewModels.ChangePreviewViewModel(preview, title);
        return ShowDialogAsync<ChangePreviewWindow, bool>(viewModel);
    }

    public async Task<IReadOnlyList<Track>?> ShowRenameAsync(
        IReadOnlyList<Track> tracks,
        string defaultPattern,
        CancellationToken cancellationToken = default)
    {
        var viewModel = ActivatorUtilities.CreateInstance<ViewModels.RenameViewModel>(_services);
        viewModel.Initialize(tracks, defaultPattern);
        var accepted = await ShowDialogAsync<RenameWindow, bool>(viewModel);
        return accepted ? viewModel.UpdatedTracks : null;
    }

    public async Task<IReadOnlyList<Track>?> ShowImportTagsAsync(
        IReadOnlyList<Track> tracks,
        string defaultPattern,
        CancellationToken cancellationToken = default)
    {
        var viewModel = ActivatorUtilities.CreateInstance<ViewModels.ImportTagsViewModel>(_services);
        viewModel.Initialize(tracks, defaultPattern);
        var accepted = await ShowDialogAsync<ImportTagsWindow, bool>(viewModel);
        return accepted ? viewModel.UpdatedTracks : null;
    }

    public async Task<AppSettings?> ShowSettingsAsync(AppSettings settings)
    {
        var viewModel = ActivatorUtilities.CreateInstance<ViewModels.SettingsViewModel>(_services);
        viewModel.Initialize(settings);
        var accepted = await ShowDialogAsync<SettingsWindow, bool>(viewModel);
        return accepted ? viewModel.UpdatedSettings : null;
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel = "Continue")
    {
        var viewModel = new ViewModels.MessageDialogViewModel(title, message, confirmLabel, "Cancel", true);
        return ShowDialogAsync<MessageWindow, bool>(viewModel);
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var viewModel = new ViewModels.MessageDialogViewModel(title, message, "Close", string.Empty, false);
        await ShowDialogAsync<MessageWindow, bool>(viewModel);
    }

    private Window Owner => _windows.MainWindow
        ?? throw new InvalidOperationException("The main window is not available.");

    private async Task<TResult> ShowDialogAsync<TWindow, TResult>(object viewModel)
        where TWindow : Window
    {
        var window = ActivatorUtilities.CreateInstance<TWindow>(_services);
        window.DataContext = viewModel;
        return await window.ShowDialog<TResult>(Owner);
    }
}
