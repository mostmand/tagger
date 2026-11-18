using System.Text.Json;
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
using Tagger.Domain.Tracks;
using Tagger.Infrastructure.Artwork;
using Tagger.Infrastructure.Files;
using Tagger.Infrastructure.Journal;
using Tagger.Infrastructure.Metadata;
using Tagger.Infrastructure.Settings;
using Tagger.Mcp;
using Tagger.Mcp.Security;

namespace Tagger.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var arguments = CliArguments.Parse(args);
        var services = BuildServices();

        try
        {
            return arguments.Command switch
            {
                "help" or "--help" or "-h" => PrintHelp(),
                "list" => await ListAsync(services, arguments),
                "read" => await ReadAsync(services, arguments),
                "write" => await WriteAsync(services, arguments),
                "from-filename" => await FromFilenameAsync(services, arguments),
                "rename" => await RenameAsync(services, arguments),
                "mcp" => await RunMcpAsync(arguments),
                "open" => PrintOpen(arguments),
                _ => Unknown(arguments.Command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
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
        return services.BuildServiceProvider();
    }

    private static async Task<int> ListAsync(IServiceProvider services, CliArguments args)
    {
        if (args.Positionals.Count == 0)
        {
            return MissingPath("list");
        }

        var library = services.GetRequiredService<ILibraryService>();
        var result = await library.LoadAsync([args.Positionals[0]], args.GetBool("recursive", true));

        if (args.GetBool("json"))
        {
            PrintJson(result);
            return 0;
        }

        foreach (var track in result.Tracks.OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{track.DisplayArtist,-28}  {track.DisplayTitle,-40}  {track.Format,-6}  {track.DurationText}");
        }

        Console.WriteLine($"{result.Tracks.Count} tracks · {result.Failures.Count} unreadable");
        return 0;
    }

    private static async Task<int> ReadAsync(IServiceProvider services, CliArguments args)
    {
        if (args.Positionals.Count == 0)
        {
            return MissingPath("read");
        }

        var reader = services.GetRequiredService<ITrackReader>();
        var track = await reader.ReadAsync(args.Positionals[0]);
        if (args.GetBool("json"))
        {
            PrintJson(track);
            return 0;
        }

        Console.WriteLine($"{track.FilePath}");
        foreach (var (field, value) in track.Tags.Values.OrderBy(x => x.Key))
        {
            Console.WriteLine($"{TagFieldCatalog.Get(field).DisplayName,-16}{value}");
        }

        return 0;
    }

    private static async Task<int> WriteAsync(IServiceProvider services, CliArguments args)
    {
        if (args.Positionals.Count == 0)
        {
            return MissingPath("write");
        }

        var changes = new TagChangeSet();
        foreach (var name in args.OptionNames)
        {
            if (name is "json" or "dry-run" or "recursive" or "quiet")
            {
                continue;
            }

            if (!TagFieldCatalog.TryGetField(name, out var field))
            {
                Console.Error.WriteLine($"warning: unknown field '{name}' was ignored");
                continue;
            }

            changes.Add(field, args.Get(name));
        }

        if (changes.IsEmpty)
        {
            Console.Error.WriteLine("error: no tag fields were provided");
            return 2;
        }

        var library = services.GetRequiredService<ILibraryService>();
        var result = await library.LoadAsync([args.Positionals[0]], args.GetBool("recursive", true));
        var tracks = result.Tracks;

        if (args.GetBool("dry-run"))
        {
            var preview = services.GetRequiredService<IChangePreviewService>().Create(tracks, changes);
            PrintJson(new
            {
                dryRun = true,
                changedTracks = preview.ChangedTrackCount,
                changedFields = preview.ChangedFieldCount,
                diff = preview.Tracks.Where(x => x.HasChanges).Select(x => new
                {
                    path = x.Track.FilePath,
                    fields = x.Diffs.Select(d => new
                    {
                        field = TagFieldCatalog.Get(d.Field).Token,
                        before = d.Before,
                        after = d.After
                    })
                })
            });
            return 0;
        }

        var saved = await services.GetRequiredService<ITagSaveService>()
            .ApplyAsync(tracks, changes, CoverArtUpdate.Keep, "CLI write");
        if (args.GetBool("json"))
        {
            PrintJson(saved);
        }
        else
        {
            Console.WriteLine($"updated {saved.UpdatedTracks.Count} tracks · {saved.Failures.Count} failures");
        }

        return saved.Failures.Count == 0 ? 0 : 1;
    }

    private static async Task<int> FromFilenameAsync(IServiceProvider services, CliArguments args)
    {
        if (args.Positionals.Count == 0)
        {
            return MissingPath("from-filename");
        }

        var patternText = args.Get("pattern") ?? "%artist% - %title%";
        if (!TagPattern.TryParse(patternText, out var pattern, out var error) || pattern is null)
        {
            Console.Error.WriteLine($"error: {error}");
            return 2;
        }

        var tracks = await LoadTracksAsync(services, args.Positionals[0], args.GetBool("recursive", true));
        var plan = services.GetRequiredService<IFilenameImportService>().CreatePlan(tracks, pattern);

        if (args.GetBool("dry-run"))
        {
            PrintJson(plan);
            return 0;
        }

        var requests = plan.Entries
            .Where(x => x.IsValid && x.HasChanges)
            .Select(x => new TagEditRequest(x.Track, x.Changes))
            .ToList();

        var saved = await services.GetRequiredService<ITagSaveService>()
            .ApplyAsync(requests, "CLI tags from filename");
        Console.WriteLine($"updated {saved.UpdatedTracks.Count} tracks · {saved.Failures.Count} failures");
        return saved.Failures.Count == 0 ? 0 : 1;
    }

    private static async Task<int> RenameAsync(IServiceProvider services, CliArguments args)
    {
        if (args.Positionals.Count == 0)
        {
            return MissingPath("rename");
        }

        var patternText = args.Get("pattern") ?? "%artist% - %title%";
        if (!TagPattern.TryParse(patternText, out var pattern, out var error) || pattern is null)
        {
            Console.Error.WriteLine($"error: {error}");
            return 2;
        }

        var tracks = await LoadTracksAsync(services, args.Positionals[0], args.GetBool("recursive", true));
        var rename = services.GetRequiredService<IRenameService>();
        var plan = rename.CreatePlan(tracks, pattern);

        if (!args.GetBool("commit") || args.GetBool("dry-run"))
        {
            PrintJson(new
            {
                dryRun = true,
                changes = plan.ChangeCount,
                errors = plan.ErrorCount,
                entries = plan.Entries.Select(x => new
                {
                    from = x.OriginalPath,
                    to = x.TargetPath,
                    valid = x.IsValid,
                    error = x.Error
                })
            });
            return plan.HasErrors ? 1 : 0;
        }

        var result = await rename.ExecuteAsync(plan, "CLI rename");
        Console.WriteLine($"renamed {result.UpdatedTracks.Count} files · {result.Failures.Count} failures");
        return result.Failures.Count == 0 ? 0 : 1;
    }

    private static async Task<IReadOnlyList<Track>> LoadTracksAsync(
        IServiceProvider services,
        string path,
        bool recursive)
    {
        var library = services.GetRequiredService<ILibraryService>();
        var result = await library.LoadAsync([path], recursive);
        return result.Tracks;
    }

    private static async Task<int> RunMcpAsync(CliArguments args)
    {
        var roots = args.GetAll("allow");
        if (roots.Count == 0)
        {
            Console.Error.WriteLine("error: mcp requires at least one --allow <directory>");
            return 2;
        }

        var services = McpBootstrapper.Build(roots);
        var host = services.GetRequiredService<McpServerHost>();
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        return await host.RunAsync(Console.In, Console.Out);
    }

    private static int PrintOpen(CliArguments args)
    {
        Console.WriteLine("Open the desktop app and use Add folder, or drag files into the library.");
        if (args.Positionals.Count > 0)
        {
            Console.WriteLine($"Folder: {Path.GetFullPath(args.Positionals[0])}");
        }

        return 0;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            tagger — cross-platform music tag editor

            Usage:
              tagger list <path> [--recursive] [--json]
              tagger read <file> [--json]
              tagger write <path> [--title X] [--artist X] [--album X] [...] [--dry-run] [--json]
              tagger from-filename <path> --pattern "%artist% - %title%" [--dry-run]
              tagger rename <path> --pattern "%artist%/%album%/%track% - %title%" [--commit]
              tagger mcp --allow <directory> [--allow <directory>]
            """);
        return 0;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"error: unknown command '{command}'. Run 'tagger help'.");
        return 2;
    }

    private static int MissingPath(string command)
    {
        Console.Error.WriteLine($"error: {command} requires a path");
        return 2;
    }

    private static void PrintJson(object value) =>
        Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
}
