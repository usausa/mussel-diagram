namespace MusselDiagram.Internal;

using System.Text.Json;

using Microsoft.JSInterop;

internal sealed class DiagramScriptBridge(MusselDiagramView view)
{
    [JSInvokable]
    public void OnViewportChanged(double zoom, double x, double y, double width, double height)
    {
        if (!view.IsDisposed)
        {
            view.OnViewportChanged(zoom, x, y, width, height);
        }
    }

    [JSInvokable]
    public Task OnSelectionChanged(string[] ids) => RunAsync(() => view.OnSelectionChangedAsync(ids));

    [JSInvokable]
    public async Task<JsonElement> CommitMoveAsync(string[] ids, double dx, double dy, bool reparent, string? parentId, string? splitLinkId, long startVersion, int rejectionsSeen, string[]? selection) =>
        ScriptJson.ToElement(await CommitAsync(() => view.CommitMoveAsync(ids, new DiagramPoint(dx, dy), reparent, parentId, splitLinkId, startVersion, rejectionsSeen, selection)).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitDropAsync(string type, double x, double y, double width, double height, string? parentId, string? value, string? splitLinkId, long startVersion, int rejectionsSeen, string[]? selection) =>
        ScriptJson.ToElement(await CommitAsync(() => view.CommitDropAsync(type, new DiagramRect(x, y, width, height), parentId, value, splitLinkId, startVersion, rejectionsSeen, selection)).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitTextAsync(string id, string slot, string text, long startVersion, int rejectionsSeen, string[]? selection) =>
        ScriptJson.ToElement(await CommitAsync(() => view.CommitTextAsync(id, slot, text, startVersion, rejectionsSeen, selection)).ConfigureAwait(true));

    [JSInvokable]
    public Task ExecuteCommandAsync(string name, string[]? selection) => RunAsync(() => view.ExecuteFromScriptAsync(name, selection));

    [JSInvokable]
    public Task OnClickedAsync(string? id, double x, double y, bool shift, bool ctrl, bool alt, bool meta, bool twice) =>
        RunAsync(() => view.OnClickedAsync(id, new DiagramPoint(x, y), shift, ctrl, alt, meta, twice));

    [JSInvokable]
    public Task OnBadgeClickedAsync(string id, int index) => RunAsync(() => view.OnBadgeClickedAsync(id, index));

    [JSInvokable]
    public JsonElement SearchTypes(string query, string[] names)
    {
        try
        {
            return ScriptJson.ToElement(view.SearchTypes(query, names));
        }
        catch (Exception ex) when (ex is not (JSDisconnectedException or OperationCanceledException))
        {
            view.Report(ex);
            throw;
        }
    }

    [JSInvokable]
    public void WantIcons(string[] ids)
    {
        if (!view.IsDisposed)
        {
            view.WantIconIds(ids);
        }
    }

    [JSInvokable]
    public async Task<JsonElement> CommitResizeAsync(string id, double x, double y, double width, double height, long startVersion, int rejectionsSeen, string[]? selection) =>
        ScriptJson.ToElement(await CommitAsync(() => view.CommitResizeAsync(id, new DiagramRect(x, y, width, height), startVersion, rejectionsSeen, selection)).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitConnectAsync(string source, string? sourcePort, string target, string? targetPort, string linkType, int rejectionsSeen, string[]? selection) =>
        ScriptJson.ToElement(await CommitAsync(() => view.CommitConnectAsync(new DiagramLinkEnd(source, sourcePort), new DiagramLinkEnd(target, targetPort), linkType, rejectionsSeen, selection)).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitReconnectAsync(string linkId, string side, string element, string? port, long startVersion, int rejectionsSeen, string[]? selection) =>
        ScriptJson.ToElement(await CommitAsync(() => view.CommitReconnectAsync(
            linkId,
            string.Equals(side, "source", StringComparison.Ordinal) ? DiagramLinkEndSide.Source : DiagramLinkEndSide.Target,
            new DiagramLinkEnd(element, port),
            startVersion,
            rejectionsSeen,
            selection)).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitPlaceAsync(string type, double x, double y, double width, double height, string? parentId, string fromElement, string? fromPort, string linkType, int rejectionsSeen, string[]? selection) =>
        ScriptJson.ToElement(await CommitAsync(() => view.CommitPlaceAsync(type, new DiagramRect(x, y, width, height), parentId, new DiagramLinkEnd(fromElement, fromPort), linkType, rejectionsSeen, selection)).ConfigureAwait(true));

    private Task RunAsync(Func<Task> operation) => view.IsDisposed ? Task.CompletedTask : view.ReportAsync(operation());

    private Task<ScriptCommitResult> CommitAsync(Func<Task<ScriptCommitResult>> operation) =>
        view.IsDisposed ? Task.FromCanceled<ScriptCommitResult>(new CancellationToken(true)) : view.ReportAsync(operation());
}
