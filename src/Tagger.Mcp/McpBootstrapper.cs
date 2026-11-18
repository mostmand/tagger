using Microsoft.Extensions.DependencyInjection;
using Tagger.Application.Abstractions;
using Tagger.Application.Artwork;
using Tagger.Application.Editing;
using Tagger.Application.Importing;
using Tagger.Application.Journal;
using Tagger.Application.Library;
using Tagger.Application.Renaming;
using Tagger.Application.Settings;
using Tagger.Domain.Naming;
using Tagger.Infrastructure.Artwork;
using Tagger.Infrastructure.Files;
using Tagger.Infrastructure.Journal;
using Tagger.Infrastructure.Metadata;
using Tagger.Infrastructure.Settings;
using Tagger.Mcp.Security;
using Tagger.Mcp.Tools;

namespace Tagger.Mcp;

public static class McpBootstrapper
{
    public static IServiceProvider Build(IReadOnlyList<string> allowedRoots)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new PathGuard(allowedRoots));

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
        services.AddSingleton<ICoverArtService, CoverArtService>();
        services.AddSingleton<IRenameService, RenameService>();
        services.AddSingleton<IFilenameImportService, FilenameImportService>();
        services.AddSingleton<IUndoService, UndoService>();
        services.AddSingleton<FilenamePatternParser>();

        services.AddSingleton<IMcpTool, LibraryScanTool>();
        services.AddSingleton<IMcpTool, TagsReadTool>();
        services.AddSingleton<IMcpTool, TagsWriteTool>();
        services.AddSingleton<IMcpTool, TagsDiffTool>();
        services.AddSingleton<IMcpTool, FilesRenameTool>();
        services.AddSingleton<IMcpTool, TagsFromFilenameTool>();
        services.AddSingleton<IMcpTool, ArtworkSetTool>();
        services.AddSingleton<IMcpTool, ChangesUndoTool>();

        services.AddSingleton<McpToolRegistry>();
        services.AddSingleton<McpServerHost>();

        return services.BuildServiceProvider();
    }
}
