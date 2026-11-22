using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Tagger.Desktop.Services;
using Tagger.Desktop.ViewModels;
using Tagger.Desktop.Views;

namespace Tagger.Desktop;

public partial class App : Avalonia.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services = Bootstrapper.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = Services.GetRequiredService<MainWindow>();
            var viewModel = Services.GetRequiredService<MainWindowViewModel>();
            window.DataContext = viewModel;
            Services.GetRequiredService<IWindowProvider>().MainWindow = window;
            desktop.MainWindow = window;

            window.Opened += async (_, _) => await viewModel.InitializeAsync();
            desktop.ShutdownRequested += (_, _) => (Services as IDisposable)?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
