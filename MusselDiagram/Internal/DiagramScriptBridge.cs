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
    public Task OnSelectionChangedAsync(string[] ids, int selectionNumber) => RunAsync(selectionNumber, () => view.OnSelectionChangedAsync(ids));

    [JSInvokable]
    public async Task<JsonElement> CommitMoveAsync(string[] ids, double dx, double dy, bool reparent, string? parentId, string? splitLinkId, long startVersion, int rejectionsSeen, string[]? selection, int selectionNumber) =>
        ScriptJson.ToElement(await RunAsync(selectionNumber, () => view.CommitMoveAsync(ids, new DiagramPoint(dx, dy), reparent, parentId, splitLinkId, startVersion, rejectionsSeen, selection)).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitDropAsync(string type, double x, double y, double width, double height, string? parentId, string? value, string? splitLinkId, long startVersion, int rejectionsSeen, string[]? selection, int selectionNumber) =>
        ScriptJson.ToElement(await RunAsync(selectionNumber, () => view.CommitDropAsync(type, new DiagramRect(x, y, width, height), parentId, value, splitLinkId, startVersion, rejectionsSeen, selection)).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitTextAsync(string id, string slot, string text, long startVersion, int rejectionsSeen, string[]? selection, int selectionNumber) =>
        ScriptJson.ToElement(await RunAsync(selectionNumber, () => view.CommitTextAsync(id, slot, text, startVersion, rejectionsSeen, selection)).ConfigureAwait(true));

    [JSInvokable]
    public Task ExecuteCommandAsync(string name, string[]? selection, int selectionNumber) => RunAsync(selectionNumber, () => view.ExecuteFromScriptAsync(name, selection));

    [JSInvokable]
    public Task OnClickedAsync(string? id, double x, double y, bool shift, bool ctrl, bool alt, bool meta, bool twice) =>
        RunAsync(() => view.OnClickedAsync(id, new DiagramPoint(x, y), shift, ctrl, alt, meta, twice));

    [JSInvokable]
    public Task OnBadgeClickedAsync(string id, int index) => RunAsync(() => view.OnBadgeClickedAsync(id, index));

    [JSInvokable]
    public Task<JsonElement> SearchTypesAsync(string query) => RunAsync(() => Task.FromResult(ScriptJson.ToElement(view.SearchTypes(query))));

    [JSInvokable]
    public void WantIcons(string[] ids)
    {
        if (!view.IsDisposed)
        {
            view.WantIconIds(ids);
        }
    }

    [JSInvokable]
    public async Task<JsonElement> CommitResizeAsync(string id, double left, double top, double right, double bottom, long startVersion, int rejectionsSeen, string[]? selection, int selectionNumber) =>
        ScriptJson.ToElement(await RunAsync(selectionNumber, () => view.CommitResizeAsync(id, new DiagramThickness(left, top, right, bottom), startVersion, rejectionsSeen, selection)).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitConnectAsync(string source, string? sourcePort, string target, string? targetPort, string linkType, int rejectionsSeen, string[]? selection, int selectionNumber) =>
        ScriptJson.ToElement(await RunAsync(selectionNumber, () => view.CommitConnectAsync(new DiagramLinkEnd(source, sourcePort), new DiagramLinkEnd(target, targetPort), linkType, rejectionsSeen, selection)).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitReconnectAsync(string linkId, string side, string element, string? port, long startVersion, int rejectionsSeen, string[]? selection, int selectionNumber) =>
        ScriptJson.ToElement(await RunAsync(selectionNumber, () => view.CommitReconnectAsync(
            linkId,
            String.Equals(side, "source", StringComparison.Ordinal) ? DiagramLinkEndSide.Source : DiagramLinkEndSide.Target,
            new DiagramLinkEnd(element, port),
            startVersion,
            rejectionsSeen,
            selection)).ConfigureAwait(true));

    [JSInvokable]
    public async Task<JsonElement> CommitPlaceAsync(string type, double x, double y, double width, double height, string? parentId, string fromElement, string? fromPort, string linkType, int rejectionsSeen, string[]? selection, int selectionNumber) =>
        ScriptJson.ToElement(await RunAsync(selectionNumber, () => view.CommitPlaceAsync(type, new DiagramRect(x, y, width, height), parentId, new DiagramLinkEnd(fromElement, fromPort), linkType, rejectionsSeen, selection)).ConfigureAwait(true));

    private Task RunAsync(Func<Task> operation) => view.IsDisposed ? Task.CompletedTask : view.ReportAsync(StartAsync(operation));

    private Task RunAsync(int selectionNumber, Func<Task> operation)
    {
        view.SelectionNumber = selectionNumber;
        return RunAsync(operation);
    }

    private Task<T> RunAsync<T>(Func<Task<T>> operation) =>
        view.IsDisposed ? Task.FromCanceled<T>(new CancellationToken(true)) : view.ReportAsync(StartAsync(operation));

    private Task<T> RunAsync<T>(int selectionNumber, Func<Task<T>> operation)
    {
        view.SelectionNumber = selectionNumber;
        return RunAsync(operation);
    }

    private static async Task StartAsync(Func<Task> operation) => await operation().ConfigureAwait(true);

    private static async Task<T> StartAsync<T>(Func<Task<T>> operation) => await operation().ConfigureAwait(true);
}
