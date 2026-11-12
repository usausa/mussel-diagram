namespace Example.Client.Components.Pages;

using Example.Client.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public sealed partial class WorkflowPage : IDisposable
{
    private static readonly string[] Methods = ["GET", "POST", "PUT", "DELETE"];

    private static readonly TimeSpan StepTime = TimeSpan.FromMilliseconds(600);

    private readonly Diagram diagram = WorkflowDiagrams.Create();

    private readonly CancellationTokenSource stop = new();

    private MusselDiagramView? view;

    private DiagramBackgroundDragMode dragMode;

    private double zoom = 1;

    private IReadOnlyList<DiagramIssue> issues = [];

    private IReadOnlyList<string> selection = [];

    private WorkflowRun? run;

    private Dictionary<string, DiagramOverlay> overlays = [];

    [Inject]
    private IJSRuntime Runtime { get; set; } = default!;

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
        issues = WorkflowDiagrams.Check(diagram);
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
        issues = WorkflowDiagrams.Check(diagram);
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnSelectionChanged(IReadOnlyList<string> ids) => selection = ids;

    private async Task RunAsync()
    {
        var current = new WorkflowRun(diagram);
        run = current;
        current.Start();
        overlays = current.Overlays();
        try
        {
            while (current.Running)
            {
                StateHasChanged();
                await Task.Delay(StepTime, stop.Token);
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
        if ((run is not null) && string.Equals(e.Badge.Kind, WorkflowDiagrams.RunsBadge, StringComparison.Ordinal) && run.Rerun(e.ElementId))
        {
            overlays = run.Overlays();
        }
    }

    private async Task ExportSvgAsync()
    {
        if (view is not null)
        {
            await DownloadAsync(await view.ExportSvgAsync(new DiagramSvgExportOptions { IncludeOverlays = true, EmbedFonts = true }), "diagram.svg", "image/svg+xml");
        }
    }

    private async Task ExportImageAsync(DiagramImageFormat format)
    {
        if (view is not null)
        {
            var png = format == DiagramImageFormat.Png;
            var result = await view.ExportImageAsync(new DiagramImageExportOptions { Format = format, Scale = 2, IncludeOverlays = true });
            await DownloadAsync(result, png ? "diagram.png" : "diagram.jpg", png ? "image/png" : "image/jpeg");
        }
    }

    private async Task DownloadAsync(DiagramExportResult result, string name, string type)
    {
        if (result.Content is not { } content)
        {
            return;
        }

        await using (content)
        {
            using var stream = new DotNetStreamReference(content, true);
            await using var module = await Runtime.InvokeAsync<IJSObjectReference>("import", "./js/download.js");
            await module.InvokeVoidAsync("download", name, type, stream);
        }
    }

    private void Rename(DiagramNode node, string? name) =>
        diagram.Replace(node with { Label = string.IsNullOrWhiteSpace(name) ? null : name });

    private void Update(DiagramNode node, object data) => diagram.Replace(node with { Data = data });

    private void OnViewportChanged(DiagramViewport viewport) => zoom = viewport.Zoom;
}
