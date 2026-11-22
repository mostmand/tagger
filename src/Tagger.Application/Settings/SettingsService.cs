using Tagger.Application.Abstractions;
using Tagger.Application.Settings;
using Tagger.Domain.Naming;

namespace Tagger.Application.Settings;

public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>Loads, validates, and persists application settings.</summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsStore _store;

    public SettingsService(ISettingsStore store)
    {
        _store = store;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _store.LoadAsync(cancellationToken);
        return Normalize(stored ?? AppSettings.Default);
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        _store.SaveAsync(Normalize(settings), cancellationToken);

    public static AppSettings Normalize(AppSettings settings)
    {
        var renamePattern = TagPattern.TryParse(settings.RenamePattern, out _, out _)
            ? settings.RenamePattern.Trim()
            : AppSettings.Default.RenamePattern;

        var autoTagPattern = TagPattern.TryParse(settings.AutoTagPattern, out _, out _)
            ? settings.AutoTagPattern.Trim()
            : AppSettings.Default.AutoTagPattern;

        return settings with
        {
            Id3Version = settings.Id3Version is 2 or 3 or 4 ? settings.Id3Version : 3,
            Padding = Math.Clamp(settings.Padding, 0, 65536),
            Id3v1 = settings.Id3v1.ToLowerInvariant() is "on" or "off" ? settings.Id3v1.ToLowerInvariant() : "auto",
            RenamePattern = renamePattern,
            AutoTagPattern = autoTagPattern,
            Theme = Enum.IsDefined(settings.Theme) ? settings.Theme : AppTheme.Dark,
            LastFolder = string.IsNullOrWhiteSpace(settings.LastFolder) ? null : settings.LastFolder.Trim(),
            PlaybackVolume = double.IsFinite(settings.PlaybackVolume)
                ? Math.Clamp(settings.PlaybackVolume, 0d, 1d)
                : AppSettings.Default.PlaybackVolume,
            PlaybackRepeat = Enum.IsDefined(settings.PlaybackRepeat)
                ? settings.PlaybackRepeat
                : Playback.RepeatMode.Off
        };
    }
}
