namespace Example.Client.Components.Pages;

using Example.Client.Models;

public sealed partial class HomePage
{
    private readonly Diagram diagram = SampleDiagrams.CreateBasic();

    private static void OnMoving(DiagramMovingEventArgs e)
    {
        if (e.ElementIds.Contains("lb"))
        {
            e.Cancel = true;
            e.Reason = "この例ではロードバランサーを動かせません";
        }
    }
}
