using System.Text;
using System.Text.RegularExpressions;
using Tagger.Domain.Tracks;

namespace Tagger.Domain.Naming;

/// <summary>
/// A filename template such as <c>%artist%/%album%/%track% - %title%</c>.
/// Rendering is pure and format agnostic.
/// </summary>
public sealed class TagPattern
{
    private static readonly Regex TokenRegex = new(
        "%([A-Za-z0-9_]+)%",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<PatternSegment> _segments;

    private TagPattern(string template, IReadOnlyList<PatternSegment> segments)
    {
        Template = template;
        _segments = segments;
        Tokens = segments.Where(x => x.Token is not null).Select(x => x.Token!).ToList();
    }

    public string Template { get; }

    public IReadOnlyList<string> Tokens { get; }

    public static TagPattern Parse(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new FormatException("Pattern cannot be empty.");
        }

        var segments = new List<PatternSegment>();
        var position = 0;

        foreach (Match match in TokenRegex.Matches(template))
        {
            if (match.Index > position)
            {
                segments.Add(new PatternSegment(template[position..match.Index], null));
            }

            var token = match.Groups[1].Value;
            if (!IsKnownToken(token))
            {
                throw new FormatException($"Unknown pattern token '%{token}%'.");
            }

            segments.Add(new PatternSegment(string.Empty, token));
            position = match.Index + match.Length;
        }

        if (position < template.Length)
        {
            segments.Add(new PatternSegment(template[position..], null));
        }

        if (segments.All(x => x.Token is null))
        {
            throw new FormatException("Pattern must contain at least one field token.");
        }

        return new TagPattern(template, segments);
    }

    public static bool TryParse(string template, out TagPattern? pattern, out string? error)
    {
        try
        {
            pattern = Parse(template);
            error = null;
            return true;
        }
        catch (FormatException ex)
        {
            pattern = null;
            error = ex.Message;
            return false;
        }
    }

    public string Render(Track track)
    {
        var builder = new StringBuilder();
        foreach (var segment in _segments)
        {
            if (segment.Token is null)
            {
                builder.Append(segment.Literal);
            }
            else
            {
                builder.Append(RenderToken(segment.Token, track));
            }
        }

        return builder.ToString();
    }

    public bool TryRender(Track track, out string result, out string? error)
    {
        try
        {
            result = Render(track);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            result = string.Empty;
            error = ex.Message;
            return false;
        }
    }

    private static string RenderToken(string token, Track track)
    {
        var normalized = token.TrimStart('_').ToLowerInvariant();

        return normalized switch
        {
            "filename" => track.FileNameWithoutExtension,
            "extension" => track.Extension.Length == 0 ? string.Empty : $".{track.Extension}",
            "ext" => track.Extension,
            "directory" => Path.GetFileName(track.DirectoryPath) ?? string.Empty,
            "track2" => PadNumber(track.Tags.Get(TagField.Track), 2),
            "track3" => PadNumber(track.Tags.Get(TagField.Track), 3),
            "disc2" => PadNumber(track.Tags.Get(TagField.Disc), 2),
            "disc3" => PadNumber(track.Tags.Get(TagField.Disc), 3),
            _ when TagFieldCatalog.TryGetByToken(normalized, out var info) => track.Tags.Get(info.Field) ?? string.Empty,
            _ => string.Empty
        };
    }

    private static string PadNumber(string? value, int width) =>
        int.TryParse(value, out var number) ? number.ToString($"D{width}") : string.Empty;

    private static bool IsKnownToken(string token)
    {
        var normalized = token.TrimStart('_').ToLowerInvariant();
        return normalized is "filename" or "extension" or "ext" or "directory"
            or "track2" or "track3" or "disc2" or "disc3"
            || TagFieldCatalog.TryGetByToken(normalized, out _);
    }

    private sealed record PatternSegment(string Literal, string? Token);
}
