namespace Example.Client.Components.Pages;

using Example.Client.Models;

using Microsoft.AspNetCore.Components;

public sealed partial class HomePage
{
    private readonly Diagram diagram = SampleDiagrams.CreateBasic();

    private MusselDiagramView? view;

    private DiagramBackgroundDragMode dragMode;

    private double zoom = 1;

    private IReadOnlyList<string> selection = [];

    [SupplyParameterFromQuery]
    public bool Wait { get; set; }

    [CascadingParameter]
    private GridMode Grid { get; set; }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            StateHasChanged();
        }
    }

    private static void OnMoving(DiagramMovingEventArgs e)
    {
        if (e.ElementIds.Contains("pin"))
        {
            e.Cancel = true;
            e.Reason = "この例ではピンを動かせません";
        }
    }

    private void OnSelectionChanged(IReadOnlyList<string> ids) => selection = ids;

    private void OnViewportChanged(DiagramViewport viewport) => zoom = viewport.Zoom;
}
