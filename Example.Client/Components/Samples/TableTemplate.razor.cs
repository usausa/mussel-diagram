namespace Example.Client.Components.Samples;

public sealed partial class TableTemplate
{
    private static string RowTransform(int row) =>
        String.Create(CultureInfo.InvariantCulture, $"translate(0,{ErDiagrams.HeaderHeight + (row * ErDiagrams.RowHeight)})");
}
