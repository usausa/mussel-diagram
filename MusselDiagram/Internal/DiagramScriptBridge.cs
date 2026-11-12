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
    public async Task<JsonElement> CommitMoveAsync(string[] ids, double dx, double dy, bool reparent, string? parentId, string? splitLinkId, long baseVersion, int rejectionsSeen, string[] selection) =>
        ScriptJson.ToElement(await view.CommitMoveAsync(ids, new DiagramPoint(dx, dy), reparent, parentId, splitLinkId, baseVersion, rejectionsSeen, selection).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitDropAsync(string type, double x, double y, double width, double height, string? parentId, string? value, string? splitLinkId, long baseVersion, int rejectionsSeen, string[] selection) =>
        ScriptJson.ToElement(await view.CommitDropAsync(type, new DiagramRect(x, y, width, height), parentId, value, splitLinkId, baseVersion, rejectionsSeen, selection).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitTextAsync(string id, string slot, string text, long baseVersion, int rejectionsSeen, string[] selection) =>
        ScriptJson.ToElement(await view.CommitTextAsync(id, slot, text, baseVersion, rejectionsSeen, selection).ConfigureAwait(true));

    [JSInvokable]
    public Task ExecuteCommandAsync(string name, string[] selection) => view.ExecuteFromScriptAsync(name, selection);

    [JSInvokable]
    public Task OnClickedAsync(string? id, double x, double y, bool shift, bool ctrl, bool alt, bool meta, bool twice) =>
        view.OnClickedAsync(id, new DiagramPoint(x, y), shift, ctrl, alt, meta, twice);

    [JSInvokable]
    public Task OnBadgeClickedAsync(string id, int index) => view.OnBadgeClickedAsync(id, index);

    [JSInvokable]
    public JsonElement SearchTypes(string query, string[] names) => ScriptJson.ToElement(view.SearchTypes(query, names));

    [JSInvokable]
    public async Task<JsonElement> CommitResizeAsync(string id, double x, double y, double width, double height, long baseVersion, int rejectionsSeen, string[] selection) =>
        ScriptJson.ToElement(await view.CommitResizeAsync(id, new DiagramRect(x, y, width, height), baseVersion, rejectionsSeen, selection).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitConnectAsync(string source, string? sourcePort, string target, string? targetPort, string linkType, long baseVersion, int rejectionsSeen, string[] selection) =>
        ScriptJson.ToElement(await view.CommitConnectAsync(new DiagramLinkEnd(source, sourcePort), new DiagramLinkEnd(target, targetPort), linkType, baseVersion, rejectionsSeen, selection).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitReconnectAsync(string linkId, string side, string element, string? port, long baseVersion, int rejectionsSeen, string[] selection) =>
        ScriptJson.ToElement(await view.CommitReconnectAsync(
            linkId,
            string.Equals(side, "source", StringComparison.Ordinal) ? DiagramLinkEndSide.Source : DiagramLinkEndSide.Target,
            new DiagramLinkEnd(element, port),
            baseVersion,
            rejectionsSeen,
            selection).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitPlaceAsync(string type, double x, double y, double width, double height, string? parentId, string fromElement, string? fromPort, string linkType, long baseVersion, int rejectionsSeen, string[] selection) =>
        ScriptJson.ToElement(await view.CommitPlaceAsync(type, new DiagramRect(x, y, width, height), parentId, new DiagramLinkEnd(fromElement, fromPort), linkType, baseVersion, rejectionsSeen, selection).ConfigureAwait(true));
}
