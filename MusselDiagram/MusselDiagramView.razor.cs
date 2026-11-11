namespace MusselDiagram;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public sealed partial class MusselDiagramView : IAsyncDisposable
{
    internal const string ModulePath = "./_content/MusselDiagram/MusselDiagram.js";

    private ElementReference root;

    private IJSObjectReference? module;

    private IJSObjectReference? controller;

    private bool disposed;

    [Parameter]
    [EditorRequired]
    public Diagram Diagram { get; set; } = default!;

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject]
    private IJSRuntime Runtime { get; set; } = default!;

    private string RootClass => AdditionalAttributes?.GetValueOrDefault("class") is { } extra ? $"mussel-diagram {extra}" : "mussel-diagram";

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        await ReleaseAsync().ConfigureAwait(true);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            module = await Runtime.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
            controller = await module.InvokeAsync<IJSObjectReference>("attach", root).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }

        if (disposed)
        {
            await ReleaseAsync().ConfigureAwait(true);
        }
    }

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
