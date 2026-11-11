namespace MusselDiagram;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MusselDiagram.Internal;

public sealed partial class MusselDiagramPalette
{
    private const float RowHeight = 32;

    private readonly HashSet<string> toggled = new(StringComparer.Ordinal);

    private List<PaletteRow> rows = [];

    private ElementReference root;

    private IJSObjectReference? module;

    private IJSObjectReference? source;

    private Diagram? subscribed;

    private string query = string.Empty;

    private bool disposed;

    [Parameter]
    [EditorRequired]
    public MusselDiagramView View { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<string>? Categories { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject]
    private IJSRuntime Runtime { get; set; } = default!;

    private bool Disabled => View.ReadOnly || !View.DropEnabled;

    private string RootClass => AdditionalAttributes?.GetValueOrDefault("class") is { } extra ? $"mussel-diagram-palette {extra}" : "mussel-diagram-palette";

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        Subscribe(null);
        try
        {
            if (source is not null)
            {
                await source.InvokeVoidAsync("dispose").ConfigureAwait(true);
                await source.DisposeAsync().ConfigureAwait(true);
            }

            if (module is not null)
            {
                await module.DisposeAsync().ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }

        source = null;
        module = null;
    }

    protected override void OnParametersSet()
    {
        Subscribe(View.Diagram);
        Build();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || disposed)
        {
            return;
        }

        try
        {
            module = await Runtime.InvokeAsync<IJSObjectReference>("import", MusselDiagramView.ModulePath).ConfigureAwait(true);
            source = await module.InvokeAsync<IJSObjectReference>("attachSource", root, View.Root).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private void Subscribe(Diagram? diagram)
    {
        if (ReferenceEquals(subscribed, diagram))
        {
            return;
        }

        if (subscribed is not null)
        {
            subscribed.Changed -= OnChanged;
            subscribed.SchemaChanged -= OnChanged;
        }

        subscribed = diagram;
        if (diagram is not null)
        {
            diagram.Changed += OnChanged;
            diagram.SchemaChanged += OnChanged;
        }
    }

    private void OnChanged(object? sender, EventArgs e) =>
        _ = InvokeAsync(() =>
        {
            Build();
            StateHasChanged();
        });

    private void OnSearch(ChangeEventArgs e)
    {
        query = e.Value as string ?? string.Empty;
        Build();
    }

    private void Toggle(string category)
    {
        if (!toggled.Remove(category))
        {
            toggled.Add(category);
        }

        Build();
    }

    private void Build()
    {
        var diagram = View.Diagram;
        var index = diagram.Index;
        var types = TypeSearch.PaletteTypes(diagram.Snapshot, index)
            .Where(x => (Categories is null) || ((x.Category is not null) && Categories.Contains(x.Category, StringComparer.Ordinal)))
            .ToList();
        var searching = query.Trim().Length > 0;
        var shown = searching ? TypeSearch.Search(types, query, index, View.SearchNormalizer) : types;
        var order = index.Schema.Categories.Select(static x => x.Name).ToList();
        var result = new List<PaletteRow>();
        foreach (var type in shown.Where(static x => x.Category is null))
        {
            result.Add(Item(type));
        }

        var categories = shown.Select(static x => x.Category).OfType<string>().Distinct(StringComparer.Ordinal)
            .OrderBy(x => order.IndexOf(x) is var i and >= 0 ? i : int.MaxValue)
            .ToList();
        foreach (var category in categories)
        {
            var definition = index.Schema.Categories.FirstOrDefault(x => string.Equals(x.Name, category, StringComparison.Ordinal));
            var open = searching || ((definition?.Collapsed ?? false) == toggled.Contains(category));
            result.Add(new PaletteRow(null, category, definition?.DisplayName ?? category, null, open));
            if (open)
            {
                result.AddRange(shown.Where(x => string.Equals(x.Category, category, StringComparison.Ordinal)).Select(Item));
            }
        }

        rows = result;

        PaletteRow Item(DiagramBoxType type) =>
            new(type, type.Category, type.DisplayName ?? type.Name, type.Look.Icon is { } icon ? View.RenderContext.FindIconId(icon) : null, true);
    }

    private sealed record PaletteRow(DiagramBoxType? Type, string? Category, string Label, string? IconId, bool Open);
}
