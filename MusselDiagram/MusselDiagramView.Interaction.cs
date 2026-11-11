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

    internal async Task<ScriptCommitResult> CommitMoveAsync(string[] ids, DiagramPoint offset, long baseVersion, int rejectionsSeen)
    {
        await commitLock.WaitAsync().ConfigureAwait(true);
        try
        {
            if (rejectionsSeen < rejections)
            {
                return new ScriptCommitResult(false, Snapshot.Version, null, rejections);
            }

            var reason = CheckMove(ids, baseVersion);
            if (reason is null)
            {
                var args = new DiagramMovingEventArgs(ids, offset, null);
                var before = Snapshot;
                await Moving.InvokeAsync(args).ConfigureAwait(true);
                if (args.Cancel)
                {
                    reason = new DiagramReason(args.Reason is null ? DiagramReasonKind.Canceled : DiagramReasonKind.App, args.Reason);
                }
                else if (!ReferenceEquals(before, Snapshot))
                {
                    reason = CheckMove(ids, baseVersion);
                }
            }

            if (reason is not null)
            {
                rejections++;
                await CommitRejected.InvokeAsync(new DiagramCommitRejectedEventArgs(DiagramOperation.Move, reason)).ConfigureAwait(true);
                return new ScriptCommitResult(false, Snapshot.Version, reason.Message ?? BuiltInTexts.Reason(reason.Kind), rejections);
            }

            Diagram.Move(ids, offset, null, DiagramChangeOrigin.User);
            var after = Snapshot;
            await Moved.InvokeAsync(new DiagramMovedEventArgs(ids, offset, null, after)).ConfigureAwait(true);
            return new ScriptCommitResult(true, after.Version, null, rejections);
        }
        finally
        {
            commitLock.Release();
        }
    }

    private DiagramReason? CheckMove(IEnumerable<string> ids, long baseVersion)
    {
        if (ReadOnly || !MoveEnabled)
        {
            return new DiagramReason(DiagramReasonKind.Permission);
        }

        var current = Snapshot;
        var origin = baseVersion == current.Version ? current : history.GetValueOrDefault(baseVersion);
        if (origin is null)
        {
            return new DiagramReason(DiagramReasonKind.Changed);
        }

        foreach (var id in ids)
        {
            if ((current.Find(id) is not DiagramBox box) || (origin.Find(id) is not DiagramBox))
            {
                return new DiagramReason(DiagramReasonKind.Changed);
            }

            if (!ScriptTables.CanMove(box, Diagram.Index))
            {
                return new DiagramReason(DiagramReasonKind.Permission);
            }

            var descendants = current.GetDescendants(id);
            if (descendants.Length != origin.GetDescendants(id).Length)
            {
                return new DiagramReason(DiagramReasonKind.Changed);
            }

            foreach (var moved in descendants.Prepend(box))
            {
                if ((origin.Find(moved.Id) is not DiagramBox old) || (old.Bounds != moved.Bounds) || !string.Equals(old.ParentId, moved.ParentId, StringComparison.Ordinal))
                {
                    return new DiagramReason(DiagramReasonKind.Changed);
                }
            }
        }

        return null;
    }

    private async Task SetSelectionAsync(IReadOnlyList<string> ids)
    {
        if (ids.SequenceEqual(SelectedIds, StringComparer.Ordinal))
        {
            return;
        }

        SelectedIds = ids;
        await SelectionChanged.InvokeAsync(SelectedIds).ConfigureAwait(true);
    }

    private void Remember(DiagramSnapshot snapshot)
    {
        history[snapshot.Version] = snapshot;
        history.Remove(snapshot.Version - HistoryLimit);
    }

    private void OnDiagramChanged(object? sender, DiagramChangedEventArgs e)
    {
        Remember(e.After);
        pendingChanges.Add(ScriptTables.Changes(e, Diagram.Index));
        if (SelectedIds.Any(e.Deleted.Contains))
        {
            SelectedIds = [.. SelectedIds.Where(x => !e.Deleted.Contains(x))];
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
