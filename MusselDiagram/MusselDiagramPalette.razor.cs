namespace MusselDiagram;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MusselDiagram.Internal;

public sealed partial class MusselDiagramPalette : IAsyncDisposable
{
    private const float RowHeight = 32;

    private readonly HashSet<string> toggled = new(StringComparer.Ordinal);

    private List<PaletteRow> rows = [];

    private ElementReference root;

    private IJSObjectReference? module;

    private IJSObjectReference? source;

    private ElementReference? attachedRoot;

    private bool attaching;

    private Diagram? subscribed;

    private MusselDiagramView? followed;

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

    private bool Attached => (attachedRoot is { } attached) && String.Equals(attached.Id, View.Root.Id, StringComparison.Ordinal);

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        Follow(null);
        Subscribe(null);
        await ReleaseAsync().ConfigureAwait(true);
    }

    protected override void OnParametersSet()
    {
        Follow(View);
        Subscribe(View.Diagram);
        Build();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed || attaching || ((module is null) && !firstRender) || Attached)
        {
            return;
        }

        attaching = true;
        try
        {
            module ??= await Runtime.InvokeAsync<IJSObjectReference>("import", MusselDiagramView.ModulePath).ConfigureAwait(true);
            while (!disposed && !Attached)
            {
                var viewRoot = View.Root;
                var previous = source;
                source = null;
                attachedRoot = viewRoot;
                await ScriptCalls.ReleaseAsync(previous, null).ConfigureAwait(true);
                source = await module.InvokeAsync<IJSObjectReference>("attachSource", root, viewRoot).ConfigureAwait(true);
            }

            if (disposed)
            {
                await ReleaseAsync().ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
        finally
        {
            attaching = false;
        }
    }

    private Task ReleaseAsync()
    {
        var attached = source;
        var loaded = module;
        source = null;
        module = null;
        return ScriptCalls.ReleaseAsync(attached, loaded);
    }

    private RenderFragment PaletteIconOf(string icon) => builder =>
    {
        builder.OpenComponent<PaletteIcon>(0);
        builder.AddComponentParameter(1, nameof(PaletteIcon.View), View);
        builder.AddComponentParameter(2, nameof(PaletteIcon.Icon), icon);
        builder.CloseComponent();
    };

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

    private void Follow(MusselDiagramView? view)
    {
        if (ReferenceEquals(followed, view))
        {
            return;
        }

        if (followed is not null)
        {
            followed.DiagramReplaced -= OnDiagramReplaced;
        }

        followed = view;
        if (view is not null)
        {
            view.DiagramReplaced += OnDiagramReplaced;
        }
    }

    private void OnChanged(object? sender, EventArgs e) =>
        _ = InvokeAsync(() =>
        {
            Build();
            StateHasChanged();
        });

    private void OnDiagramReplaced(object? sender, EventArgs e) =>
        _ = InvokeAsync(() =>
        {
            if (disposed)
            {
                return;
            }

            Subscribe(View.Diagram);
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
        var result = new List<PaletteRow>();
        foreach (var type in shown.Where(static x => x.Category is null))
        {
            result.Add(Item(type));
        }

        foreach (var category in TypeSearch.OrderCategories(shown, index))
        {
            var definition = index.Schema.Categories.FirstOrDefault(x => String.Equals(x.Name, category, StringComparison.Ordinal));
            var open = searching || ((definition?.Collapsed ?? false) == toggled.Contains(category));
            result.Add(new PaletteRow(null, category, definition?.DisplayName ?? category, null, open, DiagramShape.Rectangle, null, null));
            if (open)
            {
                result.AddRange(shown.Where(x => String.Equals(x.Category, category, StringComparison.Ordinal)).Select(Item));
            }
        }

        rows = result;

        PaletteRow Item(DiagramBoxType type)
        {
            var look = View.RenderContext.Index.LookOf(type);
            var icon = (look.Icon is { } reference) && (View.RenderContext.FindIconId(reference) is not null) ? reference : null;
            return new(type, type.Category, type.DisplayName ?? type.Name, icon, true, look.Shape, look.Path, Svg.ShapeStyle(look));
        }
    }

    private sealed record PaletteRow(DiagramBoxType? Type, string? Category, string Label, string? Icon, bool Open, DiagramShape Shape, string? Path, string? Style);
}
