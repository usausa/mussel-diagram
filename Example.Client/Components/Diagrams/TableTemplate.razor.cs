namespace Example.Client.Components.Diagrams;

using Example.Client.Models;

public sealed partial class TableTemplate
{
    private static string RowTransform(int row) =>
        string.Create(CultureInfo.InvariantCulture, $"translate(0,{ErDiagrams.HeaderHeight + (row * ErDiagrams.RowHeight)})");
}
