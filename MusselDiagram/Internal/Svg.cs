namespace MusselDiagram.Internal;

internal static class Svg
{
    public static string Number(double value) => LinkRouting.Format(value);

    public static string Translate(double x, double y) => $"translate({Number(x)},{Number(y)})";

    public static string? Dash(DiagramDash dash) => dash switch
    {
        DiagramDash.Dashed => "6 4",
        DiagramDash.Dotted => "2 3",
        _ => null
    };

    public static string? ShapeStyle(DiagramLook look)
    {
        var style = string.Empty;
        if (look.Fill is { } fill)
        {
            style += $"--mussel-diagram-shape-fill: {fill};";
        }

        if (look.Stroke is { } stroke)
        {
            style += $"--mussel-diagram-shape-stroke: {stroke};";
        }

        return style.Length == 0 ? null : style;
    }
}
