using Tagger.Application.Settings;
using Tagger.Domain.Tracks;

namespace Tagger.Desktop.Services;

public interface IThemeService
{
    AppTheme Current { get; }

    void Apply(AppTheme theme);
}

public interface IWindowProvider
{
    Avalonia.Controls.Window? MainWindow { get; set; }
}

public sealed class WindowProvider : IWindowProvider
{
    public Avalonia.Controls.Window? MainWindow { get; set; }
}

public interface IDialogService
{
    Task<IReadOnlyList<string>?> PickAudioFilesAsync(CancellationToken cancellationToken = default);

    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);

    Task<byte[]?> PickArtworkAsync(CancellationToken cancellationToken = default);

    Task<bool> ShowChangePreviewAsync(ChangePreview preview, string title);

    Task<IReadOnlyList<Track>?> ShowRenameAsync(
        IReadOnlyList<Track> tracks,
        string defaultPattern,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Track>?> ShowImportTagsAsync(
        IReadOnlyList<Track> tracks,
        string defaultPattern,
        CancellationToken cancellationToken = default);

    Task<AppSettings?> ShowSettingsAsync(AppSettings settings);

    Task<bool> ConfirmAsync(string title, string message, string confirmLabel = "Continue");

    Task ShowErrorAsync(string title, string message);
}
