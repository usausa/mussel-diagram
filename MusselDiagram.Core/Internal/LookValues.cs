namespace MusselDiagram.Internal;

using System.Text.RegularExpressions;

internal static partial class LookValues
{
    public static readonly (DiagramDash Value, string Name)[] DashNames =
    [
        (DiagramDash.Solid, "solid"),
        (DiagramDash.Dashed, "dashed"),
        (DiagramDash.Dotted, "dotted")
    ];

    public static readonly (DiagramMarker Value, string Name)[] MarkerNames =
    [
        (DiagramMarker.None, "none"),
        (DiagramMarker.Arrow, "arrow"),
        (DiagramMarker.OpenArrow, "openArrow"),
        (DiagramMarker.Circle, "circle"),
        (DiagramMarker.Diamond, "diamond")
    ];

    public static readonly (DiagramLineForm Value, string Name)[] FormNames =
    [
        (DiagramLineForm.Orthogonal, "orthogonal"),
        (DiagramLineForm.Straight, "straight"),
        (DiagramLineForm.Curved, "curved")
    ];

    private static readonly DiagramBoxLook EmptyBoxLook = new();

    private static readonly DiagramLinkLook EmptyLinkLook = new();

    public static bool IsColor(string value) => ColorPattern().IsMatch(value);

    public static bool IsWidth(double value) => Double.IsFinite(value) && (value >= 0);

    public static bool IsEmpty(DiagramBoxLook look) => look == EmptyBoxLook;

    public static bool IsEmpty(DiagramLinkLook look) => look == EmptyLinkLook;

    public static string? NameOf<T>(T? value, ReadOnlySpan<(T Value, string Name)> names)
        where T : struct
    {
        if (value is not { } known)
        {
            return null;
        }

        foreach (var (item, name) in names)
        {
            if (EqualityComparer<T>.Default.Equals(item, known))
            {
                return name;
            }
        }

        return null;
    }

    public static bool IsKnown<T>(T value, ReadOnlySpan<(T Value, string Name)> names)
        where T : struct => NameOf(value, names) is not null;

    public static bool TryRead<T>(string name, ReadOnlySpan<(T Value, string Name)> names, out T value)
        where T : struct
    {
        foreach (var (item, text) in names)
        {
            if (String.Equals(text, name, StringComparison.OrdinalIgnoreCase))
            {
                value = item;
                return true;
            }
        }

        value = default;
        return false;
    }

    [GeneratedRegex(@"^(#([0-9a-f]{3,4}|[0-9a-f]{6}|[0-9a-f]{8})|[a-z]+|var\(--[a-z0-9_-]+\)|(rgba?|hsla?|hwb|lab|lch|oklab|oklch|color|color-mix)\([0-9a-z.,% /+-]*\))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
}
