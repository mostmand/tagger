using Tagger.Domain.Tracks;

namespace Tagger.Domain.Searching;

public enum YearOperator
{
    Equal,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

public sealed record YearFilter(YearOperator Operator, int Year)
{
    public bool Matches(string? value)
    {
        if (!int.TryParse(value, out var year))
        {
            return false;
        }

        return Operator switch
        {
            YearOperator.Equal => year == Year,
            YearOperator.GreaterThan => year > Year,
            YearOperator.GreaterThanOrEqual => year >= Year,
            YearOperator.LessThan => year < Year,
            YearOperator.LessThanOrEqual => year <= Year,
            _ => false
        };
    }
}

/// <summary>
/// A simple, predictable search language: free text plus <c>field:value</c> qualifiers.
/// Unqualified text matches across the common display fields.
/// </summary>
public sealed class TrackSearchQuery
{
    private readonly IReadOnlyDictionary<TagField, string> _fields;
    private readonly YearFilter? _year;
    private readonly string? _extension;
    private readonly string? _path;
    private readonly string _text;

    private TrackSearchQuery(
        IReadOnlyDictionary<TagField, string> fields,
        YearFilter? year,
        string? extension,
        string? path,
        string text)
    {
        _fields = fields;
        _year = year;
        _extension = extension;
        _path = path;
        _text = text;
    }

    public static TrackSearchQuery Empty { get; } = new(
        new Dictionary<TagField, string>(),
        null,
        null,
        null,
        string.Empty);

    public bool IsEmpty =>
        _text.Length == 0 && _fields.Count == 0 && _year is null && _extension is null && _path is null;

    public static TrackSearchQuery Parse(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Empty;
        }

        var fields = new Dictionary<TagField, string>();
        var textParts = new List<string>();
        YearFilter? year = null;
        string? extension = null;
        string? path = null;

        foreach (var token in Tokenize(query))
        {
            var separator = token.IndexOf(':');
            if (separator <= 0 || separator == token.Length - 1)
            {
                textParts.Add(token);
                continue;
            }

            var qualifier = token[..separator].Trim().ToLowerInvariant();
            var value = token[(separator + 1)..].Trim().Trim('"');
            if (value.Length == 0)
            {
                textParts.Add(token);
                continue;
            }

            if (qualifier is "year" && TryParseYear(value, out var parsedYear))
            {
                year = parsedYear;
            }
            else if (qualifier is "ext" or "format" or "extension")
            {
                extension = value.TrimStart('.').ToUpperInvariant();
            }
            else if (qualifier is "path" or "file" or "filename")
            {
                path = value;
            }
            else if (TryMapField(qualifier, out var field))
            {
                fields[field] = value;
            }
            else
            {
                textParts.Add(token);
            }
        }

        return new TrackSearchQuery(fields, year, extension, path, string.Join(' ', textParts).Trim());
    }

    public bool Matches(Track track)
    {
        if (_extension is not null &&
            !string.Equals(track.Format, _extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_year is not null && !_year.Matches(track.Tags.Get(TagField.Year)))
        {
            return false;
        }

        if (_path is not null && !Contains(track.FilePath, _path))
        {
            return false;
        }

        foreach (var (field, value) in _fields)
        {
            var actual = track.Tags.Get(field);

            if (!Contains(actual, value))
            {
                return false;
            }
        }

        if (_text.Length == 0)
        {
            return true;
        }

        return Contains(track.Tags.Get(TagField.Title), _text) ||
               Contains(track.Tags.Get(TagField.Artist), _text) ||
               Contains(track.Tags.Get(TagField.Album), _text) ||
               Contains(track.Tags.Get(TagField.AlbumArtist), _text) ||
               Contains(track.Tags.Get(TagField.Genre), _text) ||
               Contains(track.Tags.Get(TagField.Composer), _text) ||
               Contains(track.Tags.Get(TagField.Comment), _text) ||
               Contains(track.FileName, _text) ||
               Contains(track.FilePath, _text);
    }

    private static bool Contains(string? actual, string expected) =>
        actual is not null && actual.Contains(expected, StringComparison.OrdinalIgnoreCase);

    private static bool TryMapField(string qualifier, out TagField field)
    {
        return TagFieldCatalog.TryGetField(qualifier, out field);
    }

    private static bool TryParseYear(string value, out YearFilter filter)
    {
        filter = null!;
        var op = YearOperator.Equal;
        var number = value;

        if (value.StartsWith(">=", StringComparison.Ordinal))
        {
            op = YearOperator.GreaterThanOrEqual;
            number = value[2..];
        }
        else if (value.StartsWith("<=", StringComparison.Ordinal))
        {
            op = YearOperator.LessThanOrEqual;
            number = value[2..];
        }
        else if (value.StartsWith('>'))
        {
            op = YearOperator.GreaterThan;
            number = value[1..];
        }
        else if (value.StartsWith('<'))
        {
            op = YearOperator.LessThan;
            number = value[1..];
        }

        if (!int.TryParse(number.Trim(), out var year))
        {
            return false;
        }

        filter = new YearFilter(op, year);
        return true;
    }

    private static IEnumerable<string> Tokenize(string query)
    {
        var current = new System.Text.StringBuilder();
        var quote = false;

        foreach (var ch in query)
        {
            if (ch == '"')
            {
                quote = !quote;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !quote)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
