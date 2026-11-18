namespace Tagger.Cli;

/// <summary>Small, dependency-free argument reader shared by every CLI command.</summary>
public sealed class CliArguments
{
    private readonly Dictionary<string, List<string>> _options;

    private CliArguments(
        string command,
        IReadOnlyList<string> positionals,
        Dictionary<string, List<string>> options)
    {
        Command = command;
        Positionals = positionals;
        _options = options;
    }

    public string Command { get; }

    public IReadOnlyList<string> Positionals { get; }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) =>
        _options.TryGetValue(name, out var values) ? values[^1] : null;

    public IReadOnlyList<string> GetAll(string name) =>
        _options.TryGetValue(name, out var values) ? values : [];

    public bool GetBool(string name, bool defaultValue = false)
    {
        if (!_options.TryGetValue(name, out var values))
        {
            return defaultValue;
        }

        return values[^1] is not "false" and not "0";
    }

    public IReadOnlyList<string> OptionNames => _options.Keys.ToList();

    public static CliArguments Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliArguments("help", [], new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
        }

        var command = args[0].TrimStart('-').ToLowerInvariant();
        var positionals = new List<string>();
        var options = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(token);
                continue;
            }

            var equals = token.IndexOf('=');
            string name;
            string? value = null;

            if (equals > 2)
            {
                name = token[2..equals];
                value = token[(equals + 1)..];
            }
            else
            {
                name = token[2..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = args[++i];
                }
            }

            if (!options.TryGetValue(name, out var list))
            {
                list = [];
                options[name] = list;
            }

            list.Add(value ?? "true");
        }

        return new CliArguments(command, positionals, options);
    }
}
