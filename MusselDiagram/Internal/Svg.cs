namespace MusselDiagram.Internal;

using System.Globalization;

internal static class Svg
{
    public static string Number(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    public static string Translate(double x, double y) => $"translate({Number(x)},{Number(y)})";

    public static string? Dash(DiagramDash dash) => dash switch
    {
        DiagramDash.Dashed => "6 4",
        DiagramDash.Dotted => "2 3",
        _ => null
    };
}
