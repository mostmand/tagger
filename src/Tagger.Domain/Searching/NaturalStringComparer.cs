namespace Tagger.Domain.Searching;

/// <summary>Compares strings so that "track 2" sorts before "track 10".</summary>
public sealed class NaturalStringComparer : IComparer<string>
{
    public static NaturalStringComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var xi = 0;
        var yi = 0;

        while (xi < x.Length && yi < y.Length)
        {
            if (char.IsDigit(x[xi]) && char.IsDigit(y[yi]))
            {
                var xStart = xi;
                var yStart = yi;
                while (xi < x.Length && char.IsDigit(x[xi]))
                {
                    xi++;
                }

                while (yi < y.Length && char.IsDigit(y[yi]))
                {
                    yi++;
                }

                var xNumber = x[xStart..xi].TrimStart('0');
                var yNumber = y[yStart..yi].TrimStart('0');
                var lengthCompare = xNumber.Length.CompareTo(yNumber.Length);
                if (lengthCompare != 0)
                {
                    return lengthCompare;
                }

                var numberCompare = string.CompareOrdinal(xNumber, yNumber);
                if (numberCompare != 0)
                {
                    return numberCompare;
                }

                continue;
            }

            var charCompare = char.ToUpperInvariant(x[xi]).CompareTo(char.ToUpperInvariant(y[yi]));
            if (charCompare != 0)
            {
                return charCompare;
            }

            xi++;
            yi++;
        }

        return (x.Length - xi).CompareTo(y.Length - yi);
    }
}
