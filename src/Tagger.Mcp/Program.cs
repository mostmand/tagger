using Microsoft.Extensions.DependencyInjection;

namespace Tagger.Mcp;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var roots = ParseRoots(args);
        if (roots.Count == 0)
        {
            await Console.Error.WriteLineAsync("tagger mcp requires at least one --allow <directory> root.");
            return 2;
        }

        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var services = McpBootstrapper.Build(roots);
        var host = services.GetRequiredService<McpServerHost>();
        return await host.RunAsync(Console.In, Console.Out);
    }

    private static List<string> ParseRoots(string[] args)
    {
        var roots = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--allow" && i + 1 < args.Length)
            {
                roots.Add(args[++i]);
            }
        }

        return roots;
    }
}
