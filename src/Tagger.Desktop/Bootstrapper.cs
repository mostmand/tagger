using Microsoft.Extensions.DependencyInjection;
using Tagger.Application.Abstractions;
using Tagger.Application.Artwork;
using Tagger.Application.Duplicates;
using Tagger.Application.Editing;
using Tagger.Application.Importing;
using Tagger.Application.Journal;
using Tagger.Application.Library;
using Tagger.Application.Renaming;
using Tagger.Application.Settings;
using Tagger.Desktop.Services;
using Tagger.Desktop.ViewModels;
using Tagger.Desktop.Views;
using Tagger.Domain.Naming;
using Tagger.Infrastructure.Artwork;
using Tagger.Infrastructure.Files;
using Tagger.Infrastructure.Journal;
using Tagger.Infrastructure.Metadata;
using Tagger.Infrastructure.Settings;

namespace Tagger.Desktop;

public static class Bootstrapper
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IWindowProvider, WindowProvider>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IThemeService, ThemeService>();

        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IFileScanner, AudioFileScanner>();
        services.AddSingleton<TagLibTrackRepository>();
        services.AddSingleton<ITrackReader>(x => x.GetRequiredService<TagLibTrackRepository>());
        services.AddSingleton<ITrackWriter>(x => x.GetRequiredService<TagLibTrackRepository>());
        services.AddSingleton<IImageInspector, ImageInspector>();
        services.AddSingleton<IChangeJournal>(_ => new JsonChangeJournal());
        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore());

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILibraryService, LibraryService>();
        services.AddSingleton<IChangePreviewService, ChangeSetPreviewService>();
        services.AddSingleton<ITagSaveService, TagSaveService>();
        services.AddSingleton<ISelectionSummaryService, SelectionSummaryService>();
        services.AddSingleton<ICoverArtService, CoverArtService>();
        services.AddSingleton<IRenameService, RenameService>();
        services.AddSingleton<IFilenameImportService, FilenameImportService>();
        services.AddSingleton<IUndoService, UndoService>();
        services.AddSingleton<IDuplicateFinder, DuplicateFinder>();
        services.AddSingleton<FilenamePatternParser>();

        services.AddSingleton<TagEditorViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
