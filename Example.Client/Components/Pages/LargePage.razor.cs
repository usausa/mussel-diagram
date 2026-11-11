namespace Example.Client.Components.Pages;

using Example.Client.Models;

public sealed partial class LargePage
{
    private readonly Diagram diagram = LargeDiagram.Create();

    private MusselDiagramView? view;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && (view is not null))
        {
            await view.ZoomToFitAsync();
        }
    }
}
