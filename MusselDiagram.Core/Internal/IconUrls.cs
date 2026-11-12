namespace MusselDiagram.Internal;

internal static class IconUrls
{
    public static bool IsAllowed(string value)
    {
        var colon = value.IndexOf(':', StringComparison.Ordinal);
        var end = value.AsSpan().IndexOfAny('/', '?', '#');
        if ((colon < 0) || ((end >= 0) && (end < colon)))
        {
            return value.Length > 0;
        }

        var scheme = value.AsSpan(0, colon);
        return scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
            scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
            (scheme.Equals("data", StringComparison.OrdinalIgnoreCase) && value.AsSpan(colon + 1).StartsWith("image/", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsReference(string value, DiagramSchema schema)
    {
        var colon = value.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0)
        {
            return false;
        }

        var prefix = value[..colon];
        return schema.IconSets.Any(x => string.Equals(x.Prefix, prefix, StringComparison.Ordinal));
    }
}
