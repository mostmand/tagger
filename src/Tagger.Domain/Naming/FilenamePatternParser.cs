using System.Text;
using System.Text.RegularExpressions;
using Tagger.Domain.Tracks;

namespace Tagger.Domain.Naming;

public sealed record FilenameParseResult(bool Success, TrackTags Tags, string? Error)
{
    public static FilenameParseResult Failed(string error) => new(false, TrackTags.Empty, error);
}

/// <summary>Parses a filename back into tag values using the inverse of a filename pattern.</summary>
public sealed class FilenamePatternParser
{
    private static readonly Regex TokenRegex = new(
        "%([A-Za-z0-9_]+)%",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public FilenameParseResult Parse(string fileName, TagPattern pattern)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return FilenameParseResult.Failed("Filename is empty.");
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var (regex, tokens, error) = BuildRegex(pattern.Template);
        if (regex is null)
        {
            return FilenameParseResult.Failed(error ?? "Invalid pattern.");
        }

        var match = regex.Match(stem);
        if (!match.Success)
        {
            return FilenameParseResult.Failed($"Filename does not match the pattern '{pattern.Template}'.");
        }

        var values = new Dictionary<TagField, string?>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var group = match.Groups[$"f{i}"];
            if (!group.Success)
            {
                continue;
            }

            var value = group.Value.Trim();
            if (value.Length == 0)
            {
                continue;
            }

            if (!TryMapToken(tokens[i], out var info))
            {
                continue;
            }

            if (info.Field is TagField.Track or TagField.Disc && TrySplitTotal(value, out var number, out var total))
            {
                values[info.Field] = number;
                values[info.Field == TagField.Track ? TagField.TrackTotal : TagField.DiscTotal] = total;
            }
            else
            {
                values[info.Field] = value;
            }
        }

        return new FilenameParseResult(true, new TrackTags(values), null);
    }

    private static (Regex? Regex, IReadOnlyList<string> Tokens, string? Error) BuildRegex(string template)
    {
        var tokens = new List<string>();
        var builder = new StringBuilder("^");
        var position = 0;

        foreach (Match match in TokenRegex.Matches(template))
        {
            builder.Append(Regex.Escape(template[position..match.Index]));

            var token = match.Groups[1].Value.TrimStart('_').ToLowerInvariant();
            if (!IsParsableToken(token))
            {
                return (null, [], $"Token '%{match.Groups[1].Value}%' cannot be read from a filename.");
            }

            tokens.Add(token);
            builder.Append($"(?<f{tokens.Count - 1}>.*?)");
            position = match.Index + match.Length;
        }

        builder.Append(Regex.Escape(template[position..]));
        builder.Append('$');

        return (new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), tokens, null);
    }

    private static bool IsParsableToken(string token) =>
        token is "track2" or "track3" or "disc2" or "disc3"
        || TagFieldCatalog.TryGetByToken(token, out _);

    private static bool TryMapToken(string token, out TagFieldInfo info)
    {
        if (token is "track2" or "track3")
        {
            return TagFieldCatalog.TryGetByToken("track", out info);
        }

        if (token is "disc2" or "disc3")
        {
            return TagFieldCatalog.TryGetByToken("disc", out info);
        }

        return TagFieldCatalog.TryGetByToken(token, out info);
    }

    private static bool TrySplitTotal(string value, out string number, out string total)
    {
        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            number = parts[0];
            total = parts[1];
            return true;
        }

        number = value;
        total = string.Empty;
        return false;
    }
}
