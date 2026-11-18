namespace Example.Client.Components.Pages;

using Microsoft.AspNetCore.Components;

public sealed partial class LargePage
{
    private readonly Diagram diagram = LargeDiagram.Create();

    private MusselDiagramView? view;

    private DiagramBackgroundDragMode dragMode;

    private DiagramTool tool;

    private double zoom = 1;

    [CascadingParameter]
    private GridMode Grid { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && (view is not null))
        {
            StateHasChanged();
            await view.ZoomToFitAsync();
        }
    }

    private void OnViewportChanged(DiagramViewport viewport) => zoom = viewport.Zoom;
}
