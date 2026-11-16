namespace MusselDiagram;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public sealed partial class MusselDiagramDragSource
{
    private ElementReference root;

    private IJSObjectReference? module;

    private IJSObjectReference? source;

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

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        await ReleaseAsync().ConfigureAwait(true);
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
            if (disposed)
            {
                await ReleaseAsync().ConfigureAwait(true);
                return;
            }

            source = await module.InvokeAsync<IJSObjectReference>("attachSource", root, View.Root).ConfigureAwait(true);
            if (disposed)
            {
                await ReleaseAsync().ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async Task ReleaseAsync()
    {
        var attached = source;
        var loaded = module;
        source = null;
        module = null;
        try
        {
            if (attached is not null)
            {
                await attached.InvokeVoidAsync("dispose").ConfigureAwait(true);
                await attached.DisposeAsync().ConfigureAwait(true);
            }

            if (loaded is not null)
            {
                await loaded.DisposeAsync().ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
