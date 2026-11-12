namespace MusselDiagram.Internal;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

internal sealed record SanitizedSvg(string Body, double Width, double Height, IReadOnlyList<string> Removed, IReadOnlyList<string> RejectedUrls);

internal static partial class SvgSanitizer
{
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

    private static readonly object Marker = new();

    private static readonly ConditionalWeakTable<DiagramIcon, object> Imported = [];

    private static readonly ConditionalWeakTable<DiagramIcon, StrongBox<string>> Cleaned = [];

    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "svg", "g", "defs", "symbol", "use", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "text", "tspan",
        "linearGradient", "radialGradient", "stop", "clipPath", "mask", "pattern",
        "filter", "feBlend", "feColorMatrix", "feComposite", "feFlood", "feGaussianBlur", "feMerge", "feMergeNode", "feOffset"
    };

    private static readonly HashSet<string> SilentElements = new(StringComparer.Ordinal) { "title", "desc", "metadata" };

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.Ordinal)
    {
        "id", "x", "y", "x1", "y1", "x2", "y2", "cx", "cy", "r", "rx", "ry", "fx", "fy", "fr", "width", "height", "d", "points", "transform", "viewBox",
        "preserveAspectRatio", "offset", "gradientUnits", "gradientTransform", "spreadMethod", "patternUnits", "patternContentUnits", "patternTransform",
        "clipPathUnits", "maskUnits", "maskContentUnits", "filterUnits", "primitiveUnits", "stdDeviation", "dx", "dy", "in", "in2", "result", "mode",
        "operator", "values", "type", "k1", "k2", "k3", "k4", "flood-color", "flood-opacity", "fill", "fill-opacity", "fill-rule", "stroke", "stroke-width",
        "stroke-linecap", "stroke-linejoin", "stroke-miterlimit", "stroke-dasharray", "stroke-dashoffset", "stroke-opacity", "opacity", "stop-color",
        "stop-opacity", "clip-path", "clip-rule", "mask", "filter", "color", "display", "visibility", "font-family", "font-size", "font-weight", "font-style",
        "text-anchor", "dominant-baseline", "letter-spacing", "vector-effect", "shape-rendering", "paint-order", "transform-origin", "style", "href"
    };

    private static readonly string[] ForbiddenStyle = ["expression", "javascript:", "@import", "behavior", "-moz-binding"];

    public static SanitizedSvg? FromFile(string text, string idPrefix)
    {
        if (Parse(text) is not { } root)
        {
            return null;
        }

        var (width, height, minX, minY) = SizeOf(root);
        var sanitized = Sanitize(root, idPrefix);
        var body = (minX == 0) && (minY == 0) ? sanitized.Body : $"<g transform=\"translate({Number(-minX)},{Number(-minY)})\">{sanitized.Body}</g>";
        return sanitized with { Body = body, Width = width, Height = height };
    }

    public static string Body(DiagramIconSet set, DiagramIcon icon) =>
        Imported.TryGetValue(icon, out _)
            ? icon.Body
            : Cleaned.GetValue(icon, x => new StrongBox<string>(FromBody(x.Body, $"{SafeId(set.Prefix)}-{SafeId(x.Id)}-", x.Width, x.Height)?.Body ?? string.Empty)).Value!;

    public static DiagramIcon MarkImported(DiagramIcon icon)
    {
        Imported.AddOrUpdate(icon, Marker);
        return icon;
    }

    public static SanitizedSvg? FromBody(string body, string idPrefix, double width, double height)
    {
        if (Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">{body}</svg>") is not { } root)
        {
            return null;
        }

        return Sanitize(root, idPrefix) with { Width = width, Height = height };
    }

    public static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

    public static string SafeId(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) || (c is '-' or '_') ? c : '_');
        }

        return builder.ToString();
    }

    private static XElement? Parse(string text)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), settings);
            var root = XElement.Load(reader);
            return string.Equals(root.Name.LocalName, "svg", StringComparison.Ordinal) ? root : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static (double Width, double Height, double MinX, double MinY) SizeOf(XElement root)
    {
        if (root.Attribute("viewBox")?.Value is { } viewBox)
        {
            var parts = viewBox.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if ((parts.Length == 4) &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minX) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minY) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) &&
                double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var height) &&
                (width > 0) && (height > 0))
            {
                return (width, height, minX, minY);
            }
        }

        return (Length(root.Attribute("width")?.Value) ?? 24, Length(root.Attribute("height")?.Value) ?? 24, 0, 0);
    }

    private static double? Length(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^2];
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && (result > 0) ? result : null;
    }

    private static SanitizedSvg Sanitize(XElement root, string idPrefix)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in root.Descendants())
        {
            if (element.Attribute("id")?.Value is { Length: > 0 } id)
            {
                ids[id] = idPrefix + SafeId(id);
            }
        }

        var removed = new List<string>();
        var rejected = new List<string>();
        var builder = new StringBuilder();
        foreach (var child in root.Elements())
        {
            Write(child, builder, ids, removed, rejected);
        }

        return new SanitizedSvg(builder.ToString(), 0, 0, removed, rejected);
    }

    private static void Write(XElement element, StringBuilder builder, Dictionary<string, string> ids, List<string> removed, List<string> rejected)
    {
        var name = element.Name.LocalName;
        if ((element.Name.Namespace != XNamespace.None) && (element.Name.Namespace != (XNamespace)"http://www.w3.org/2000/svg"))
        {
            removed.Add(name);
            return;
        }

        if (!AllowedElements.Contains(name))
        {
            if (!SilentElements.Contains(name))
            {
                removed.Add(name);
            }

            return;
        }

        builder.Append('<').Append(name);
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            var attributeName = attribute.Name.Namespace == XLink ? attribute.Name.LocalName : attribute.Name.Namespace == XNamespace.None ? attribute.Name.LocalName : null;
            if ((attributeName is null) || !AllowedAttributes.Contains(attributeName))
            {
                if (attribute.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                {
                    removed.Add(attribute.Name.LocalName);
                }

                continue;
            }

            if (Clean(attributeName, attribute.Value, ids, rejected) is { } value)
            {
                builder.Append(' ').Append(attributeName).Append("=\"").Append(Escape(value)).Append('"');
            }
        }

        var children = element.Nodes().ToList();
        if (children.Count == 0)
        {
            builder.Append("/>");
            return;
        }

        builder.Append('>');
        foreach (var node in children)
        {
            switch (node)
            {
                case XElement child:
                    Write(child, builder, ids, removed, rejected);
                    break;
                case XText text when name is "text" or "tspan":
                    builder.Append(Escape(text.Value));
                    break;
            }
        }

        builder.Append("</").Append(name).Append('>');
    }

    private static string? Clean(string name, string value, Dictionary<string, string> ids, List<string> rejected)
    {
        switch (name)
        {
            case "id":
                return ids.GetValueOrDefault(value, value);
            case "href":
                if (value.StartsWith('#') && ids.TryGetValue(value[1..], out var target))
                {
                    return "#" + target;
                }

                rejected.Add(value);
                return null;
            case "style":
                return CleanStyle(value, ids, rejected);
        }

        var cleaned = CleanUrls(value, ids, rejected, out var ok);
        return ok ? cleaned : null;
    }

    private static string? CleanStyle(string value, Dictionary<string, string> ids, List<string> rejected)
    {
        var kept = new List<string>();
        foreach (var declaration in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (ForbiddenStyle.Any(x => declaration.Contains(x, StringComparison.OrdinalIgnoreCase)))
            {
                rejected.Add(declaration);
                continue;
            }

            var cleaned = CleanUrls(declaration, ids, rejected, out var ok);
            if (ok)
            {
                kept.Add(cleaned);
            }
        }

        return kept.Count == 0 ? null : string.Join(';', kept);
    }

    private static string CleanUrls(string value, Dictionary<string, string> ids, List<string> rejected, out bool ok)
    {
        var valid = true;
        var result = UrlPattern().Replace(value, match =>
        {
            var target = match.Groups[1].Value.Trim().Trim('\'', '"');
            if (target.StartsWith('#') && ids.TryGetValue(target[1..], out var id))
            {
                return $"url(#{id})";
            }

            rejected.Add(target);
            valid = false;
            return match.Value;
        });
        ok = valid;
        return result;
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);

    [GeneratedRegex(@"url\(([^)]*)\)", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();
}
