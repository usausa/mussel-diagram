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
}
