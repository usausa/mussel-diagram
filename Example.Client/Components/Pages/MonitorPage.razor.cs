namespace Example.Client.Components.Pages;

using Microsoft.AspNetCore.Components;

public sealed partial class MonitorPage : IDisposable
{
    private readonly Diagram diagram = MonitorDiagrams.Create();

    private readonly CancellationTokenSource stop = new();

    private readonly MonitorSimulation simulation;

    private MusselDiagramView? view;

    private DiagramBackgroundDragMode dragMode;

    private DiagramTool tool;

    private double zoom = 1;

    private Dictionary<string, DiagramOverlay> overlays = [];

    private IReadOnlyList<string> selection = [];

    public MonitorPage()
    {
        simulation = new MonitorSimulation(diagram);
    }

    [CascadingParameter]
    private GridMode Grid { get; set; }

    private IEnumerable<DiagramNode> Devices => diagram.Snapshot.Nodes;

    private DiagramNode? Selected => (selection.Count == 1) && (diagram.Find(selection[0]) is DiagramNode node) ? node : null;

    public void Dispose()
    {
        stop.Cancel();
        stop.Dispose();
    }

    protected override void OnInitialized() => overlays = simulation.Next();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && (view is not null))
        {
            StateHasChanged();
            var token = stop.Token;
            await view.ZoomToFitAsync();
            _ = RunAsync(token);
        }
    }

    private static string StateText(string state) => state switch
    {
        "warning" => "注意",
        "down" => "停止",
        _ => "正常"
    };

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.5));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                overlays = simulation.Next();
                StateHasChanged();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private string? TypeName(DiagramNode device) =>
        diagram.Schema.NodeTypes.FirstOrDefault(x => String.Equals(x.Name, device.Type, StringComparison.Ordinal))?.DisplayName;

    private void OnSelectionChanged(IReadOnlyList<string> ids) => selection = ids;

    private async Task FocusAsync(string id)
    {
        if (view is null)
        {
            return;
        }

        await view.SelectAsync([id]);
        await view.CenterOnAsync(id);
    }

    private void OnViewportChanged(DiagramViewport viewport) => zoom = viewport.Zoom;
}
