namespace MusselDiagram;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

using MusselDiagram.Internal;

public sealed partial class MusselDiagramView : IAsyncDisposable
{
    internal const string ModulePath = "./_content/MusselDiagram/MusselDiagram.js";

    private const double FitMargin = 20;

    private readonly string viewId = DiagramIds.Create()[..8];

    private IJSObjectReference? module;

    private IJSObjectReference? controller;

    private DotNetObjectReference<DiagramScriptBridge>? self;

    private Diagram? subscribed;

    private SchemaIndex? renderedIndex;

    private bool renderedTooltips;

    private DiagramRect? area;

    private string? sentOptions;

    private bool tableChanged;

    private bool schemaChanged;

    private Func<IJSObjectReference, ValueTask>? pendingView;

    private bool disposed;

    [Parameter]
    [EditorRequired]
    public Diagram Diagram { get; set; } = default!;

    [Parameter]
    public bool PanEnabled { get; set; } = true;

    [Parameter]
    public bool ZoomEnabled { get; set; } = true;

    [Parameter]
    public DiagramWheelMode WheelMode { get; set; }

    [Parameter]
    public double MinZoom { get; set; } = 0.05;

    [Parameter]
    public double MaxZoom { get; set; } = 8;

    [Parameter]
    public EventCallback<DiagramViewport> ViewportChanged { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    public DiagramViewport? Viewport { get; private set; }

    public double Zoom => Viewport?.Zoom ?? 1;

    public DiagramPoint Center => Viewport?.Center ?? default;

    internal RenderContext RenderContext { get; private set; } = default!;

    internal ElementReference Root { get; private set; }

    [Inject]
    private IJSRuntime Runtime { get; set; } = default!;

    private DiagramSnapshot Snapshot => Diagram.Snapshot;

    private string RootClass => AdditionalAttributes?.GetValueOrDefault("class") is { } extra ? $"mussel-diagram {extra}" : "mussel-diagram";

    private RenderFragment Defs => builder =>
    {
        builder.OpenComponent<DiagramDefsView>(0);
        builder.AddComponentParameter(1, nameof(DiagramDefsView.Context), RenderContext);
        builder.CloseComponent();
    };

    private RenderFragment Groups => builder =>
    {
        foreach (var group in Snapshot.Groups)
        {
            if ((group.Kind == DiagramGroupKind.Tree) && IsShown(group.Id, group.Bounds))
            {
                AddBox(builder, group);
            }
        }
    };

    private RenderFragment OverlayGroups => builder =>
    {
        foreach (var group in Snapshot.Groups)
        {
            if ((group.Kind == DiagramGroupKind.Overlay) && IsShown(group.Id, group.Bounds))
            {
                AddBox(builder, group);
            }
        }
    };

    private RenderFragment Links => builder =>
    {
        foreach (var link in Snapshot.Links)
        {
            if (!IsShown(link.Id, RouteBounds(link)))
            {
                continue;
            }

            builder.OpenComponent<DiagramLinkView>(0);
            builder.SetKey(link.Id);
            builder.AddComponentParameter(1, nameof(DiagramLinkView.Link), link);
            builder.AddComponentParameter(2, nameof(DiagramLinkView.Context), RenderContext);
            builder.CloseComponent();
        }
    };

    private RenderFragment Nodes => builder =>
    {
        foreach (var node in Snapshot.Nodes)
        {
            if (IsShown(node.Id, node.Bounds))
            {
                AddBox(builder, node);
            }
        }
    };

    public ValueTask SetViewportAsync(double zoom, DiagramPoint center) =>
        RunViewAsync(current => current.InvokeVoidAsync("setView", zoom, center.X, center.Y));

    public ValueTask ZoomToFitAsync()
    {
        var boxes = Snapshot.Groups.Cast<DiagramBox>().Concat(Snapshot.Nodes).Select(static x => x.Bounds).ToList();
        if (boxes.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        var left = boxes.Min(static x => x.X);
        var top = boxes.Min(static x => x.Y);
        var bounds = new ScriptBounds(left, top, boxes.Max(static x => x.Right) - left, boxes.Max(static x => x.Bottom) - top);
        return RunViewAsync(current => current.InvokeVoidAsync("fit", ScriptJson.ToElement(bounds), FitMargin));
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        Subscribe(null);
        await ReleaseAsync().ConfigureAwait(true);
        self?.Dispose();
        self = null;
        commitLock.Dispose();
    }

    internal void OnViewportChanged(double zoom, double x, double y, double width, double height)
    {
        var visible = new DiagramRect(-x / zoom, -y / zoom, width / zoom, height / zoom);
        Viewport = new DiagramViewport(zoom, visible.Center, visible);
        area = new DiagramRect(visible.X - (visible.Width / 2), visible.Y - (visible.Height / 2), visible.Width * 2, visible.Height * 2);
        StateHasChanged();
        _ = ViewportChanged.InvokeAsync(Viewport);
    }

    protected override void OnParametersSet()
    {
        Subscribe(Diagram);
        RefreshContext();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        try
        {
            var options = ScriptJson.ToElement(CreateOptions());
            if (firstRender)
            {
                module = await Runtime.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
                self ??= DotNetObjectReference.Create(new DiagramScriptBridge(this));
                sentOptions = options.GetRawText();
                tableChanged = false;
                schemaChanged = false;
                pendingChanges.Clear();
                var table = ScriptJson.ToElement(ScriptTables.Build(Snapshot, Diagram.Index));
                var schema = ScriptJson.ToElement(ScriptTables.Schema(RenderContext));
                controller = await module.InvokeAsync<IJSObjectReference>("attach", Root, self, options, table, schema).ConfigureAwait(true);
                await SyncCommandsAsync(controller).ConfigureAwait(true);
                if (pendingView is not null)
                {
                    var view = pendingView;
                    pendingView = null;
                    await view(controller).ConfigureAwait(true);
                }

                return;
            }

            if (controller is null)
            {
                return;
            }

            if (!string.Equals(sentOptions, options.GetRawText(), StringComparison.Ordinal))
            {
                sentOptions = options.GetRawText();
                await controller.InvokeVoidAsync("setOptions", options).ConfigureAwait(true);
            }

            if (schemaChanged)
            {
                schemaChanged = false;
                await controller.InvokeVoidAsync("setSchema", ScriptJson.ToElement(ScriptTables.Schema(RenderContext))).ConfigureAwait(true);
            }

            var changed = tableChanged;
            tableChanged = false;
            await SyncScriptAsync(controller, changed).ConfigureAwait(true);
            await SyncCommandsAsync(controller).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
        finally
        {
            if (disposed)
            {
                await ReleaseAsync().ConfigureAwait(true);
            }
        }
    }

    private static DiagramRect RouteBounds(DiagramLink link)
    {
        if (link.Route.IsDefaultOrEmpty)
        {
            return default;
        }

        var left = link.Route.Min(static p => p.X);
        var top = link.Route.Min(static p => p.Y);
        return new DiagramRect(left, top, link.Route.Max(static p => p.X) - left, link.Route.Max(static p => p.Y) - top);
    }

    private bool IsShown(string id, DiagramRect bounds) =>
        (area is not { } visible) || bounds.Intersects(visible) || SelectedIds.Contains(id, StringComparer.Ordinal);

    private async ValueTask RunViewAsync(Func<IJSObjectReference, ValueTask> command)
    {
        if (controller is null)
        {
            pendingView = command;
            return;
        }

        try
        {
            await command(controller).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private void AddBox(RenderTreeBuilder builder, DiagramBox box)
    {
        builder.OpenComponent<DiagramBoxView>(0);
        builder.SetKey(box.Id);
        builder.AddComponentParameter(1, nameof(DiagramBoxView.Box), box);
        builder.AddComponentParameter(2, nameof(DiagramBoxView.Context), RenderContext);
        builder.CloseComponent();
    }

    private void Subscribe(Diagram? diagram)
    {
        if (ReferenceEquals(subscribed, diagram))
        {
            return;
        }

        if (subscribed is not null)
        {
            subscribed.Changed -= OnDiagramChanged;
            subscribed.SchemaChanged -= OnSchemaChanged;
        }

        subscribed = diagram;
        history.Clear();
        pendingChanges.Clear();
        if (diagram is not null)
        {
            diagram.Changed += OnDiagramChanged;
            diagram.SchemaChanged += OnSchemaChanged;
            Remember(diagram.Snapshot);
            tableChanged = true;
        }
    }

    private void OnSchemaChanged(object? sender, EventArgs e) =>
        _ = InvokeAsync(() =>
        {
            RefreshContext();
            StateHasChanged();
        });

    private void RefreshContext()
    {
        if (ReferenceEquals(renderedIndex, Diagram.Index) && (renderedTooltips == TooltipEnabled))
        {
            return;
        }

        renderedIndex = Diagram.Index;
        renderedTooltips = TooltipEnabled;
        RenderContext = new RenderContext(Diagram.Index, viewId, TooltipEnabled);
        tableChanged = true;
        schemaChanged = true;
    }

    private ScriptOptions CreateOptions() => new()
    {
        MinZoom = MinZoom,
        MaxZoom = MaxZoom,
        WheelMode = WheelMode == DiagramWheelMode.Zoom ? "zoom" : "pan",
        PanEnabled = PanEnabled,
        ZoomEnabled = ZoomEnabled,
        SelectionEnabled = SelectionEnabled,
        BoxSelectionEnabled = SelectionEnabled && BoxSelectionEnabled,
        MoveEnabled = MoveEnabled && !ReadOnly,
        ResizeEnabled = ResizeEnabled && !ReadOnly,
        ConnectEnabled = ConnectEnabled && !ReadOnly,
        LivePreviewLimit = LivePreviewLimit,
        Optimistic = CommitMode == DiagramCommitMode.Optimistic,
        GridVisible = GridVisible,
        GridStyle = GridStyle == DiagramGridStyle.Lines ? "lines" : "dots",
        GridSize = GridSize,
        SnapToGrid = SnapToGrid,
        SnapToElements = SnapToElements,
        SnapTolerance = SnapTolerance,
        ConnectionRadius = ConnectionRadius,
        QuickAddSpacing = QuickAddSpacing,
        LinkType = LinkType,
        DropEnabled = DropEnabled && !ReadOnly,
        TextEditEnabled = TextEditEnabled && !ReadOnly,
        TooltipEnabled = TooltipEnabled,
        HostSearch = SearchNormalizer is not null
    };

    private async Task ReleaseAsync()
    {
        var currentController = controller;
        var currentModule = module;
        controller = null;
        module = null;
        try
        {
            if (currentController is not null)
            {
                await currentController.InvokeVoidAsync("dispose").ConfigureAwait(true);
                await currentController.DisposeAsync().ConfigureAwait(true);
            }

            if (currentModule is not null)
            {
                await currentModule.DisposeAsync().ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
