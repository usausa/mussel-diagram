namespace Example.Client.Components.Pages;

using Microsoft.AspNetCore.Components;

public sealed partial class WorkflowPage : IDisposable
{
    private static readonly string[] Methods = ["GET", "POST", "PUT", "DELETE"];

    private static readonly TimeSpan StepTime = TimeSpan.FromMilliseconds(600);

    private readonly Diagram diagram = WorkflowDiagrams.Create();

    private readonly CancellationTokenSource stop = new();

    private MusselDiagramView? view;

    private DiagramBackgroundDragMode dragMode;

    private DiagramTool tool;

    private double zoom = 1;

    private IReadOnlyList<DiagramIssue> issues = [];

    private IReadOnlyList<string> selection = [];

    private WorkflowRun? run;

    private Dictionary<string, DiagramOverlay> overlays = [];

    [CascadingParameter]
    private GridMode Grid { get; set; }

    private DiagramNode? Selected => (selection.Count == 1) && (diagram.Find(selection[0]) is DiagramNode node) ? node : null;

    public void Dispose()
    {
        diagram.Changed -= OnDiagramChanged;
        stop.Cancel();
        stop.Dispose();
    }

    protected override void OnInitialized()
    {
        issues = diagram.Validate();
        diagram.Changed += OnDiagramChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && (view is not null))
        {
            StateHasChanged();
            await view.ZoomToFitAsync();
        }
    }

    private void OnDiagramChanged(object? sender, DiagramChangedEventArgs e)
    {
        issues = diagram.Validate();
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnSelectionChanged(IReadOnlyList<string> ids) => selection = ids;

    private async Task RunAsync()
    {
        var token = stop.Token;
        var current = new WorkflowRun(diagram);
        run = current;
        current.Start();
        overlays = current.Overlays();
        try
        {
            while (current.Running)
            {
#pragma warning disable BL0012
                StateHasChanged();
#pragma warning restore BL0012
                await Task.Delay(StepTime, token);
                current.Advance();
                overlays = current.Overlays();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnBadgeClicked(DiagramBadgeClickedEventArgs e)
    {
        if ((run is not null) && String.Equals(e.Badge.Kind, WorkflowDiagrams.RunsBadge, StringComparison.Ordinal) && run.Rerun(e.ElementId))
        {
            overlays = run.Overlays();
        }
    }

    private void Rename(DiagramNode node, string? name) =>
        diagram.Replace(node with { Label = String.IsNullOrWhiteSpace(name) ? null : name });

    private void Update(DiagramNode node, object data) => diagram.Replace(node with { Data = data });

    private void OnViewportChanged(DiagramViewport viewport) => zoom = viewport.Zoom;
}
