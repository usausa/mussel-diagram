namespace Example.Client.Components.Pages;

using Example.Client.Models;

using Microsoft.AspNetCore.Components;

public sealed partial class ErPage : IDisposable
{
    private readonly Diagram diagram = ErDiagrams.Create();

    private MusselDiagramView? view;

    private DiagramBackgroundDragMode dragMode;

    private double zoom = 1;

    private IReadOnlyList<DiagramIssue> issues = [];

    private IReadOnlyList<string> selection = [];

    [CascadingParameter]
    private GridMode Grid { get; set; }

    private DiagramNode? Selected => (selection.Count == 1) && (diagram.Find(selection[0]) is DiagramNode node) ? node : null;

    public void Dispose() => diagram.Changed -= OnDiagramChanged;

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

    private void Rename(DiagramNode table, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            diagram.Replace(table with { Label = name });
        }
    }

    private void Change(DiagramNode table, ColumnData column) =>
        Store(table, [.. ErDiagrams.ColumnsOf(table.Data).Select(x => x.Id == column.Id ? column : x)]);

    private void Remove(DiagramNode table, ColumnData column) =>
        Store(table, [.. ErDiagrams.ColumnsOf(table.Data).Where(x => x.Id != column.Id)]);

    private void AddColumn(DiagramNode table)
    {
        var columns = ErDiagrams.ColumnsOf(table.Data);
        Store(table, [.. columns, new ColumnData(ErDiagrams.NextId(columns), "column", "text", false)]);
    }

    private void Store(DiagramNode table, IReadOnlyList<ColumnData> columns) =>
        diagram.Replace(table with { Data = new TableData(columns), Bounds = table.Bounds with { Height = ErDiagrams.HeightOf(columns.Count) } });

    private void OnViewportChanged(DiagramViewport viewport) => zoom = viewport.Zoom;
}
