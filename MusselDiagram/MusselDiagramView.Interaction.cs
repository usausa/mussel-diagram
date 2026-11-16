namespace MusselDiagram;

using System.Runtime.CompilerServices;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MusselDiagram.Internal;

public sealed partial class MusselDiagramView
{
    private const int HistoryLimit = 64;

    private static readonly ConditionalWeakTable<Diagram, SemaphoreSlim> Gates = [];

    private static readonly AsyncLocal<GateTurn?> CurrentTurn = new();

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

    private SemaphoreSlim Gate => Gates.GetValue(Diagram, static _ => new SemaphoreSlim(1, 1));

    public async ValueTask SelectAsync(IEnumerable<string> ids)
    {
        string[] list = [.. ids.Where(IsSelectable).Distinct(StringComparer.Ordinal)];
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

    internal Task OnSelectionChangedAsync(IEnumerable<string> ids) => SetSelectionAsync([.. ids.Where(IsSelectable)]);

    internal async Task ReportAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not (JSDisconnectedException or OperationCanceledException))
        {
            Report(ex);
            throw;
        }
    }

    internal async Task<T> ReportAsync<T>(Task<T> task)
    {
        try
        {
            return await task.ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not (JSDisconnectedException or OperationCanceledException))
        {
            Report(ex);
            throw;
        }
    }

    internal void Report(Exception exception)
    {
        if (!disposed)
        {
            _ = DispatchExceptionAsync(exception);
        }
    }

    private static async Task<T> HandleAsync<T>(GateTurn? turn, Func<Task<T>> handler)
    {
        if (turn is null)
        {
            return await handler().ConfigureAwait(true);
        }

        turn.Handling = true;
        try
        {
            return await handler().ConfigureAwait(true);
        }
        finally
        {
            turn.Handling = false;
        }
    }

    private static async Task HandleAsync(GateTurn? turn, Func<Task> handler)
    {
        if (turn is null)
        {
            await handler().ConfigureAwait(true);
            return;
        }

        turn.Handling = true;
        try
        {
            await handler().ConfigureAwait(true);
        }
        finally
        {
            turn.Handling = false;
        }
    }

    private async Task<GateTurn?> EnterGateAsync()
    {
        var gate = Gate;
        if ((CurrentTurn.Value is { Handling: true } turn) && ReferenceEquals(turn.Gate, gate))
        {
            return null;
        }

        await gate.WaitAsync().ConfigureAwait(true);
        return new GateTurn(gate);
    }

    private async Task NotifyAsync<T>(EventCallback<T> callback, T args)
    {
        try
        {
            await callback.InvokeAsync(args).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not (JSDisconnectedException or OperationCanceledException))
        {
            Report(ex);
        }
    }

    private bool IsSelectable(string id) => Snapshot.Find(id) is DiagramBox or DiagramLink;

    private async Task SetSelectionAsync(IReadOnlyList<string> ids)
    {
        if (ids.SequenceEqual(SelectedIds, StringComparer.Ordinal))
        {
            return;
        }

        SelectedIds = ids;
        if (HasCulledShown())
        {
            StateHasChanged();
        }

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
            pendingChanges.Add(ScriptTables.Changes(e, Diagram.Index, templates));
        }

        if (SelectedIds.Any(x => !e.After.Contains(x)))
        {
            SelectedIds = [.. SelectedIds.Where(e.After.Contains)];
            _ = NotifyAsync(SelectionChanged, SelectedIds);
        }

        _ = InvokeAsync(() =>
        {
            WantIcons(e);
            StateHasChanged();
        });
    }

    private async Task SyncScriptAsync(IJSObjectReference current, bool resend)
    {
        try
        {
            if (resend)
            {
                pendingChanges.Clear();
                await current.InvokeVoidAsync("setTable", ScriptJson.ToElement(ScriptTables.Build(Snapshot, Diagram.Index, templates))).ConfigureAwait(true);
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
                if (disposed)
                {
                    return;
                }
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private sealed class GateTurn(SemaphoreSlim gate)
    {
        public SemaphoreSlim Gate => gate;

        public bool Handling { get; set; }

        public void Begin() => CurrentTurn.Value = this;

        public void End()
        {
            Handling = false;
            CurrentTurn.Value = null;
            gate.Release();
        }
    }
}
