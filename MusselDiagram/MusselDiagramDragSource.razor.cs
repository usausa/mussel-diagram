namespace MusselDiagram;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MusselDiagram.Internal;

public sealed partial class MusselDiagramDragSource : IAsyncDisposable
{
    private ElementReference root;

    private IJSObjectReference? module;

    private IJSObjectReference? source;

    private ElementReference? attachedRoot;

    private bool attaching;

    private bool disposed;

    [Parameter]
    [EditorRequired]
    public MusselDiagramView View { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public string Type { get; set; } = default!;

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject]
    private IJSRuntime Runtime { get; set; } = default!;

    private string RootClass => AdditionalAttributes?.GetValueOrDefault("class") is { } extra ? $"mussel-diagram-drag-source {extra}" : "mussel-diagram-drag-source";

    private bool Attached => (attachedRoot is { } attached) && String.Equals(attached.Id, View.Root.Id, StringComparison.Ordinal);

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        await ReleaseAsync().ConfigureAwait(true);
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
}
