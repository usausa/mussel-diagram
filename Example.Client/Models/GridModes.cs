namespace Example.Client.Models;

public static class GridModes
{
    public static bool IsVisible(this GridMode mode) => mode != GridMode.None;

    public static GridMode Next(this GridMode mode) => mode == GridMode.None ? GridMode.Lines : GridMode.None;

    public static string Name(this GridMode mode) => mode == GridMode.Lines ? "lines" : "none";
}
