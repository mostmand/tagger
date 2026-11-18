namespace Tagger.Domain.Tracks;

public sealed record TagValidationResult(IReadOnlyDictionary<TagField, string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static TagValidationResult Valid { get; } = new(new Dictionary<TagField, string>());
}

/// <summary>Pure validation rules shared by every surface.</summary>
public static class TagValidator
{
    public static TagValidationResult Validate(TrackTags tags, int currentYear)
    {
        var errors = new Dictionary<TagField, string>();

        foreach (var (field, value) in tags.Values)
        {
            var error = ValidateField(field, value, currentYear);
            if (error is not null)
            {
                errors[field] = error;
            }
        }

        ValidatePair(tags, TagField.Track, TagField.TrackTotal, errors);
        ValidatePair(tags, TagField.Disc, TagField.DiscTotal, errors);

        return new TagValidationResult(errors);
    }

    public static string? ValidateField(TagField field, string? value, int currentYear)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length > 4096)
        {
            return "Value is too long.";
        }

        if (value.Any(ch => char.IsControl(ch) && ch is not '\r' and not '\n' and not '\t'))
        {
            return "Value contains control characters.";
        }

        return field switch
        {
            TagField.Year or TagField.OriginalYear => ValidateYear(value, currentYear),
            TagField.Track or TagField.TrackTotal or TagField.Disc or TagField.DiscTotal
                => ParsePositive(value, "Enter a whole number greater than zero."),
            TagField.Bpm => ValidateBpm(value),
            _ => null
        };
    }

    private static void ValidatePair(
        TrackTags tags,
        TagField number,
        TagField total,
        IDictionary<TagField, string> errors)
    {
        if (!TryParseInt(tags.Get(number), out var numberValue) ||
            !TryParseInt(tags.Get(total), out var totalValue))
        {
            return;
        }

        if (totalValue < numberValue)
        {
            errors[total] = "Total cannot be lower than the number.";
        }
    }

    private static string? ValidateYear(string value, int currentYear)
    {
        if (!int.TryParse(value, out var year))
        {
            return "Enter a four-digit year.";
        }

        return year is < 1000 || year > currentYear + 1
            ? $"Enter a year between 1000 and {currentYear + 1}."
            : null;
    }

    private static string? ValidateBpm(string value)
    {
        if (!int.TryParse(value, out var bpm))
        {
            return "Enter a whole number.";
        }

        return bpm is < 1 or > 999 ? "Enter a BPM between 1 and 999." : null;
    }

    private static string? ParsePositive(string value, string message) =>
        int.TryParse(value, out var number) && number > 0 ? null : message;

    private static bool TryParseInt(string? value, out int result) =>
        int.TryParse(value, out result);
}
