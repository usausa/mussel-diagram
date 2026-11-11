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
    public async Task<JsonElement> CommitMoveAsync(string[] ids, double dx, double dy, bool reparent, string? parentId, long baseVersion, int rejectionsSeen) =>
        ScriptJson.ToElement(await view.CommitMoveAsync(ids, new DiagramPoint(dx, dy), reparent, parentId, baseVersion, rejectionsSeen).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitResizeAsync(string id, double x, double y, double width, double height, long baseVersion, int rejectionsSeen) =>
        ScriptJson.ToElement(await view.CommitResizeAsync(id, new DiagramRect(x, y, width, height), baseVersion, rejectionsSeen).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitConnectAsync(string source, string? sourcePort, string target, string? targetPort, string linkType, long baseVersion, int rejectionsSeen) =>
        ScriptJson.ToElement(await view.CommitConnectAsync(new DiagramLinkEnd(source, sourcePort), new DiagramLinkEnd(target, targetPort), linkType, baseVersion, rejectionsSeen).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitReconnectAsync(string linkId, string side, string element, string? port, long baseVersion, int rejectionsSeen) =>
        ScriptJson.ToElement(await view.CommitReconnectAsync(
            linkId,
            string.Equals(side, "source", StringComparison.Ordinal) ? DiagramLinkEndSide.Source : DiagramLinkEndSide.Target,
            new DiagramLinkEnd(element, port),
            baseVersion,
            rejectionsSeen).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitPlaceAsync(string type, double x, double y, double width, double height, string? parentId, string fromElement, string? fromPort, string linkType, long baseVersion, int rejectionsSeen) =>
        ScriptJson.ToElement(await view.CommitPlaceAsync(type, new DiagramRect(x, y, width, height), parentId, new DiagramLinkEnd(fromElement, fromPort), linkType, baseVersion, rejectionsSeen).ConfigureAwait(true));
}
