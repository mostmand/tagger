namespace Tagger.Application.Settings;

public enum AppTheme
{
    Dark,
    Light,
    System
}

public sealed record AppSettings
{
    public int Id3Version { get; init; } = 3;

    public int Padding { get; init; } = 2048;

    public string Id3v1 { get; init; } = "auto";

    public string RenamePattern { get; init; } = "%artist% - %title%";

    public string AutoTagPattern { get; init; } = "%artist% - %title%";

    public bool Recursive { get; init; } = true;

    public string? LastFolder { get; init; }

    public AppTheme Theme { get; init; } = AppTheme.Dark;

    public bool AlwaysPreview { get; init; } = true;

    public bool BackupBeforeWrite { get; init; } = true;

    public static AppSettings Default { get; } = new();
}
