using CommunityToolkit.Mvvm.ComponentModel;
using Tagger.Application.Settings;
using Tagger.Domain.Naming;

namespace Tagger.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private AppSettings _original = AppSettings.Default;

    [ObservableProperty]
    private string _renamePattern = AppSettings.Default.RenamePattern;

    [ObservableProperty]
    private string _autoTagPattern = AppSettings.Default.AutoTagPattern;

    [ObservableProperty]
    private bool _recursive = true;

    [ObservableProperty]
    private AppTheme _theme = AppTheme.Dark;

    [ObservableProperty]
    private bool _alwaysPreview = true;

    [ObservableProperty]
    private bool _backupBeforeWrite = true;

    [ObservableProperty]
    private int _id3Version = 3;

    [ObservableProperty]
    private int _padding = 2048;

    [ObservableProperty]
    private string _id3v1 = "auto";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public IReadOnlyList<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>();

    public IReadOnlyList<int> Id3Versions { get; } = [2, 3, 4];

    public IReadOnlyList<string> Id3v1Options { get; } = ["auto", "on", "off"];

    public AppSettings? UpdatedSettings { get; private set; }

    public void Initialize(AppSettings settings)
    {
        _original = settings;
        RenamePattern = settings.RenamePattern;
        AutoTagPattern = settings.AutoTagPattern;
        Recursive = settings.Recursive;
        Theme = settings.Theme;
        AlwaysPreview = settings.AlwaysPreview;
        BackupBeforeWrite = settings.BackupBeforeWrite;
        Id3Version = settings.Id3Version;
        Padding = settings.Padding;
        Id3v1 = settings.Id3v1;
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!TagPattern.TryParse(RenamePattern, out _, out var renameError))
        {
            ErrorMessage = $"Rename pattern: {renameError}";
            return false;
        }

        if (!TagPattern.TryParse(AutoTagPattern, out _, out var autoError))
        {
            ErrorMessage = $"Filename pattern: {autoError}";
            return false;
        }

        IsBusy = true;
        try
        {
            var settings = _original with
            {
                RenamePattern = RenamePattern.Trim(),
                AutoTagPattern = AutoTagPattern.Trim(),
                Recursive = Recursive,
                Theme = Theme,
                AlwaysPreview = AlwaysPreview,
                BackupBeforeWrite = BackupBeforeWrite,
                Id3Version = Id3Version,
                Padding = Padding,
                Id3v1 = Id3v1
            };

            await _settingsService.SaveAsync(settings, cancellationToken);
            UpdatedSettings = settings;
            ErrorMessage = null;
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
