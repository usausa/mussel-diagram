namespace Example.Client.Components.Pages;

using Example.Client.Models;

using Microsoft.AspNetCore.Components;

public sealed partial class ElectricPage : IDisposable
{
    private readonly DateOnly today = DateOnly.FromDateTime(DateTime.Today);

    private readonly Diagram diagram;

    private MusselDiagramView? view;

    private DiagramBackgroundDragMode dragMode;

    private double zoom = 1;

    private IReadOnlyList<DiagramIssue> issues = [];

    private Dictionary<string, DiagramOverlay> overlays = [];

    private IReadOnlyList<string> selection = [];

    public ElectricPage()
    {
        diagram = ElectricDiagrams.Create(today);
    }

    [CascadingParameter]
    private GridMode Grid { get; set; }

    private DiagramNode? Selected => (selection.Count == 1) && (diagram.Find(selection[0]) is DiagramNode node) ? node : null;

    private IEnumerable<DiagramNode> Equipment =>
        diagram.Snapshot.Nodes.Where(static x => x.Data is EquipmentData).OrderBy(static x => x.Bounds.Y).ThenBy(static x => x.Bounds.X);

    public void Dispose() => diagram.Changed -= OnDiagramChanged;

    protected override void OnInitialized()
    {
        Refresh();
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

    private static string? DateText(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly? ParseDate(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static string NextText(DiagramNode node) => node.Data is EquipmentData { NextInspection: { } next } ? DateText(next)! : "—";

    private void Refresh()
    {
        issues = ElectricDiagrams.Check(diagram);
        overlays = ElectricDiagrams.Overlays(diagram, today);
    }

    private void OnDiagramChanged(object? sender, DiagramChangedEventArgs e)
    {
        Refresh();
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnSelectionChanged(IReadOnlyList<string> ids) => selection = ids;

    private void OnViewportChanged(DiagramViewport viewport) => zoom = viewport.Zoom;

    private string? TypeName(DiagramNode node) =>
        diagram.Schema.NodeTypes.FirstOrDefault(x => string.Equals(x.Name, node.Type, StringComparison.Ordinal))?.DisplayName;

    private string StatusName(DiagramNode node) => ElectricDiagrams.StatusOf(node.Data as EquipmentData, today) switch
    {
        InspectionStatus.Expired => "expired",
        InspectionStatus.Due => "due",
        _ => "ok"
    };

    private void Rename(DiagramNode node, string? name) =>
        diagram.Replace(node with { Label = string.IsNullOrWhiteSpace(name) ? null : name });

    private void Update(DiagramNode node, EquipmentData data) => diagram.Replace(node with { Data = data });

    private void SetFeeders(DiagramNode node, int feeders) =>
        diagram.Replace(node with { Data = new BusData(feeders), Bounds = node.Bounds with { Width = ElectricDiagrams.WidthOf(node.Type, feeders) } });

    private async Task FocusAsync(string id)
    {
        if (view is null)
        {
            return;
        }

        await view.SelectAsync([id]);
        await view.CenterOnAsync(id);
    }
}
