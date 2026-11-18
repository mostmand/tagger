using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Tagger.Application.Settings;

namespace Tagger.Desktop.Services;

public sealed class ThemeService : IThemeService
{
    private static readonly Uri DarkUri = new("avares://tagger/Themes/Dark.axaml");
    private static readonly Uri LightUri = new("avares://tagger/Themes/Light.axaml");

    private ResourceDictionary? _applied;

    public AppTheme Current { get; private set; } = AppTheme.Dark;

    public void Apply(AppTheme theme)
    {
        Current = theme;
        if (Avalonia.Application.Current is not { } application)
        {
            return;
        }

        var dictionary = (ResourceDictionary)AvaloniaXamlLoader.Load(
            theme == AppTheme.Light ? LightUri : DarkUri);

        if (_applied is not null)
        {
            application.Resources.MergedDictionaries.Remove(_applied);
        }

        application.Resources.MergedDictionaries.Add(dictionary);
        _applied = dictionary;

        application.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.System => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };
    }
}
