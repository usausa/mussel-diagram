namespace MusselDiagram;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MusselDiagram.Internal;

public sealed partial class MusselDiagramView
{
    private const int HistoryLimit = 64;

    private readonly SemaphoreSlim commitLock = new(1, 1);

    private readonly Dictionary<long, DiagramSnapshot> history = [];

    private readonly List<ScriptChanges> pendingChanges = [];

    private int rejections;

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public bool SelectionEnabled { get; set; } = true;

    [Parameter]
    public bool MoveEnabled { get; set; } = true;

    [Parameter]
    public DiagramCommitMode CommitMode { get; set; }

    [Parameter]
    public int LivePreviewLimit { get; set; } = 32;

    [Parameter]
    public EventCallback<IReadOnlyList<string>> SelectionChanged { get; set; }

    [Parameter]
    public EventCallback<DiagramMovingEventArgs> Moving { get; set; }

    [Parameter]
    public EventCallback<DiagramMovedEventArgs> Moved { get; set; }

    [Parameter]
    public EventCallback<DiagramCommitRejectedEventArgs> CommitRejected { get; set; }

    public IReadOnlyList<string> SelectedIds { get; private set; } = [];

    public async ValueTask SelectAsync(IEnumerable<string> ids)
    {
        string[] list = [.. ids.Where(Snapshot.Contains).Distinct(StringComparer.Ordinal)];
        await SetSelectionAsync(list).ConfigureAwait(true);
        if (controller is null)
        {
            return;
        }

        try
        {
            await controller.InvokeVoidAsync("select", [list]).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public ValueTask ClearSelectionAsync() => SelectAsync([]);

    internal Task OnSelectionChangedAsync(IEnumerable<string> ids) => SetSelectionAsync([.. ids.Where(Snapshot.Contains)]);

    private async Task SetSelectionAsync(IReadOnlyList<string> ids)
    {
        if (ids.SequenceEqual(SelectedIds, StringComparer.Ordinal))
        {
            return;
        }

        SelectedIds = ids;
        await SelectionChanged.InvokeAsync(SelectedIds).ConfigureAwait(true);
        await RefreshCommandsAsync().ConfigureAwait(true);
    }

    private void Remember(DiagramSnapshot snapshot)
    {
        history[snapshot.Version] = snapshot;
        history.Remove(snapshot.Version - HistoryLimit);
    }

    private void OnDiagramChanged(object? sender, DiagramChangedEventArgs e)
    {
        Remember(e.After);
        if (e.IsReset)
        {
            pendingChanges.Clear();
            tableChanged = true;
        }
        else
        {
            pendingChanges.Add(ScriptTables.Changes(e, Diagram.Index));
        }

        if (SelectedIds.Any(x => !e.After.Contains(x)))
        {
            SelectedIds = [.. SelectedIds.Where(e.After.Contains)];
            _ = SelectionChanged.InvokeAsync(SelectedIds);
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private async Task SyncScriptAsync(IJSObjectReference current, bool resend)
    {
        if (resend)
        {
            pendingChanges.Clear();
            await current.InvokeVoidAsync("setTable", ScriptJson.ToElement(ScriptTables.Build(Snapshot, Diagram.Index))).ConfigureAwait(true);
            return;
        }

        if (pendingChanges.Count == 0)
        {
            return;
        }

        ScriptChanges[] changes = [.. pendingChanges];
        pendingChanges.Clear();
        foreach (var change in changes)
        {
            await current.InvokeVoidAsync("applyChanges", ScriptJson.ToElement(change)).ConfigureAwait(true);
        }
    }
}
