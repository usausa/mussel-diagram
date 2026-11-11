namespace MusselDiagram.Internal;

using System.Text.Json;

using Microsoft.JSInterop;

internal sealed class DiagramScriptBridge(MusselDiagramView view)
{
    [JSInvokable]
    public void OnViewportChanged(double zoom, double x, double y, double width, double height) => view.OnViewportChanged(zoom, x, y, width, height);

    [JSInvokable]
    public Task OnSelectionChanged(string[] ids) => view.OnSelectionChangedAsync(ids);

    [JSInvokable]
    public async Task<JsonElement> CommitMoveAsync(string[] ids, double dx, double dy, long baseVersion, int rejectionsSeen) =>
        ScriptJson.ToElement(await view.CommitMoveAsync(ids, new DiagramPoint(dx, dy), baseVersion, rejectionsSeen).ConfigureAwait(true));
}
