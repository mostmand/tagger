namespace Tagger.Domain.Naming;

/// <summary>Produces safe file and directory names for every supported platform.</summary>
public static class FileNameSanitizer
{
    private static readonly char[] InvalidChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            if (char.IsControl(ch) || InvalidChars.Contains(ch))
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(ch);
            }
        }

        var sanitized = builder.ToString().Trim().TrimEnd('.');
        if (sanitized.Length == 0)
        {
            return string.Empty;
        }

        if (ReservedNames.Contains(sanitized))
        {
            sanitized = $"_{sanitized}";
        }

        return sanitized.Length > 160 ? sanitized[..160].TrimEnd().TrimEnd('.') : sanitized;
    }

    /// <summary>Sanitizes a relative path, discarding traversal and empty segments.</summary>
    public static string SanitizeRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var segments = value
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x is not "." and not "..")
            .Select(SanitizeSegment)
            .Where(x => x.Length > 0)
            .ToArray();

        return string.Join("/", segments);
    }
}
