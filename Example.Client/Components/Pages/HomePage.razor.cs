namespace Example.Client.Components.Pages;

using Example.Client.Models;

using Microsoft.AspNetCore.Components;

public sealed partial class HomePage
{
    private readonly Diagram diagram = SampleDiagrams.CreateBasic();

    private MusselDiagramView? view;

    [SupplyParameterFromQuery]
    public bool Wait { get; set; }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            StateHasChanged();
        }
    }

    private static void OnMoving(DiagramMovingEventArgs e)
    {
        if (e.ElementIds.Contains("lb"))
        {
            e.Cancel = true;
            e.Reason = "この例ではロードバランサーを動かせません";
        }
    }
}
