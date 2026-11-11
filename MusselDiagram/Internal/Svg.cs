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
}
