namespace MusselDiagram;

using System.Text.Json;

using Microsoft.JSInterop;

using MusselDiagram.Internal;

public sealed partial class MusselDiagramView
{
    private string? sentCommands;

    private string? sentPalette;

    private DiagramCommandRegistry? commands;

    public DiagramCommandRegistry Commands => commands ??= CreateCommands();

    private bool Editable => !ReadOnly;

    public ValueTask ZoomToSelectionAsync() => RunViewAsync(current => current.InvokeVoidAsync("fitSelection", FitMargin));

    public ValueTask CenterOnAsync(string id) => RunViewAsync(current => current.InvokeVoidAsync("centerOn", id));

    public ValueTask OpenTypePickerAsync(DiagramPoint? position = null) =>
        RunViewAsync(current => position is { } point ? current.InvokeVoidAsync("quickAdd", point.X, point.Y) : current.InvokeVoidAsync("quickAdd"));

    public ValueTask FocusAsync() => RunViewAsync(current => current.InvokeVoidAsync("focus"));

    internal async Task ExecuteFromScriptAsync(string name, IEnumerable<string> selection)
    {
        await SetSelectionAsync([.. selection.Where(Snapshot.Contains)]).ConfigureAwait(true);
        await Commands.ExecuteAsync(name).ConfigureAwait(true);
        await RefreshCommandsAsync().ConfigureAwait(true);
    }

    private DiagramCommandRegistry CreateCommands()
    {
        commands = new DiagramCommandRegistry(argument => new DiagramCommandContext(this, SelectedIds, argument));
        Register(DiagramCommands.Undo, ["Ctrl+Z"], _ => Editable && Diagram.CanUndo, _ => WriteAsync(() => Diagram.Undo()));
        Register(DiagramCommands.Redo, ["Ctrl+Y", "Ctrl+Shift+Z"], _ => Editable && Diagram.CanRedo, _ => WriteAsync(() => Diagram.Redo()));
        Register(DiagramCommands.Delete, ["Delete", "Backspace"], c => Editable && (Deletion.Deletable(Snapshot, Diagram.Index, c.SelectedIds).Count > 0), c => DeleteAsync(c.SelectedIds, false));
        Register(DiagramCommands.Cut, ["Ctrl+X"], c => Editable && (Clipboard.Copy(Snapshot, Diagram.Index, c.SelectedIds) is not null) && (Deletion.Deletable(Snapshot, Diagram.Index, c.SelectedIds).Count > 0), CutAsync);
        Register(DiagramCommands.Copy, ["Ctrl+C"], c => Clipboard.Copy(Snapshot, Diagram.Index, c.SelectedIds) is not null, CopyAsync);
        Register(DiagramCommands.Paste, ["Ctrl+V"], _ => Editable && (clipboard is not null), _ => PasteAsync());
        Register(DiagramCommands.SelectAll, ["Ctrl+A"], _ => SelectionEnabled, _ => RunViewAsync(current => current.InvokeVoidAsync("selectAll")).AsTask(), true);
        Register(DiagramCommands.ClearSelection, ["Ctrl+Shift+A"], c => c.SelectedIds.Count > 0, _ => ClearSelectionAsync().AsTask(), true);
        Register(DiagramCommands.EditText, ["F2", "Enter"], c => Editable && TextEditEnabled && (FirstEditableSlot(c.SelectedIds) is not null), c => RunViewAsync(current => current.InvokeVoidAsync("editText", c.SelectedIds[0], FirstEditableSlot(c.SelectedIds))).AsTask());
        Register(DiagramCommands.QuickAdd, ["Ctrl+Enter"], c => Editable && DropEnabled && (c.SelectedIds.Count == 0), c => OpenTypePickerAsync(c.Argument as DiagramPoint?).AsTask());
        Register(DiagramCommands.Group, ["Ctrl+G"], c => Editable && Grouping.CanGroup(Snapshot, Diagram.Index, c.SelectedIds, c.Argument as string), c => GroupAsync(Grouping.Group(Snapshot, Diagram.Index, c.SelectedIds, c.Argument as string)));
        Register(DiagramCommands.Ungroup, ["Ctrl+Shift+U"], c => Editable && Grouping.CanUngroup(Snapshot, Diagram.Index, c.SelectedIds), c => GroupAsync(Grouping.Ungroup(Snapshot, Diagram.Index, c.SelectedIds)));
        Register(DiagramCommands.CreateOverlayGroup, [], c => Editable && Grouping.CanCreateOverlay(Snapshot, Diagram.Index, c.SelectedIds, c.Argument as string), c => GroupAsync(Grouping.CreateOverlay(Snapshot, Diagram.Index, c.SelectedIds, c.Argument as string)));
        Register(DiagramCommands.AddMembers, [], c => Editable && Grouping.CanAddMembers(Snapshot, c.SelectedIds), c => GroupAsync(Grouping.AddMembers(Snapshot, Diagram.Index, c.SelectedIds)));
        Register(DiagramCommands.RemoveMembers, [], c => Editable && Grouping.CanRemoveMembers(Snapshot, c.SelectedIds), c => GroupAsync(Grouping.RemoveMembers(Snapshot, c.SelectedIds)));
        Register(DiagramCommands.BringToFront, ["Ctrl+Shift+F"], c => CanReorder(c.SelectedIds), c => ReorderAsync(c.SelectedIds, OrderMove.Front));
        Register(DiagramCommands.SendToBack, ["Ctrl+Shift+B"], c => CanReorder(c.SelectedIds), c => ReorderAsync(c.SelectedIds, OrderMove.Back));
        Register(DiagramCommands.BringForward, ["Ctrl+Alt+Shift+F"], c => CanReorder(c.SelectedIds), c => ReorderAsync(c.SelectedIds, OrderMove.Forward));
        Register(DiagramCommands.SendBackward, ["Ctrl+Alt+Shift+B"], c => CanReorder(c.SelectedIds), c => ReorderAsync(c.SelectedIds, OrderMove.Backward));
        RegisterAlign(DiagramCommands.AlignLeft, AlignEdge.Left);
        RegisterAlign(DiagramCommands.AlignCenter, AlignEdge.Center);
        RegisterAlign(DiagramCommands.AlignRight, AlignEdge.Right);
        RegisterAlign(DiagramCommands.AlignTop, AlignEdge.Top);
        RegisterAlign(DiagramCommands.AlignMiddle, AlignEdge.Middle);
        RegisterAlign(DiagramCommands.AlignBottom, AlignEdge.Bottom);
        Register(DiagramCommands.DistributeHorizontally, [], c => CanArrange(c.SelectedIds, 3), c => ArrangeAsync(Arrangement.Distribute(Snapshot, Diagram.Index, c.SelectedIds, true)));
        Register(DiagramCommands.DistributeVertically, [], c => CanArrange(c.SelectedIds, 3), c => ArrangeAsync(Arrangement.Distribute(Snapshot, Diagram.Index, c.SelectedIds, false)));
        commands.Register(new DiagramCommand
        {
            Name = DiagramCommands.ToggleLock,
            DisplayName = BuiltInTexts.Command(DiagramCommands.ToggleLock),
            Keys = ["Ctrl+L"],
            CanExecute = c => (subscribed is not null) && Editable && (Lockable(c.SelectedIds).Count > 0),
            GetIsChecked = c => Lockable(c.SelectedIds) is { Count: > 0 } items && items.All(IsLocked),
            Execute = c => ToggleLockAsync(c.SelectedIds)
        });
        Register(DiagramCommands.ZoomIn, ["Ctrl+Plus"], _ => ZoomEnabled, _ => RunViewAsync(current => current.InvokeVoidAsync("zoomStep", 1)).AsTask(), true);
        Register(DiagramCommands.ZoomOut, ["Ctrl+Minus"], _ => ZoomEnabled, _ => RunViewAsync(current => current.InvokeVoidAsync("zoomStep", -1)).AsTask(), true);
        Register(DiagramCommands.ResetZoom, ["Ctrl+0"], _ => ZoomEnabled, _ => RunViewAsync(current => current.InvokeVoidAsync("resetZoom")).AsTask(), true);
        Register(DiagramCommands.ZoomToFit, ["Ctrl+Shift+H"], _ => true, _ => ZoomToFitAsync().AsTask(), true);
        Register(DiagramCommands.ZoomToSelection, [], c => c.SelectedIds.Count > 0, _ => ZoomToSelectionAsync().AsTask(), true);
        return commands;
    }

    private void Register(string name, string[] keys, Func<DiagramCommandContext, bool> canExecute, Func<DiagramCommandContext, Task> execute, bool local = false) =>
        commands!.Register(new DiagramCommand { Name = name, DisplayName = BuiltInTexts.Command(name), Keys = keys, CanExecute = c => (subscribed is not null) && canExecute(c), Execute = execute, Local = local });

    private void RegisterAlign(string name, AlignEdge edge) =>
        Register(name, [], c => CanArrange(c.SelectedIds, 2), c => ArrangeAsync(Arrangement.Align(Snapshot, Diagram.Index, c.SelectedIds, edge)));

    private bool CanArrange(IEnumerable<string> selection, int minimum) =>
        Editable && MoveEnabled && Arrangement.CanAlign(Snapshot, Diagram.Index, selection, minimum);

    private bool CanReorder(IEnumerable<string> selection) =>
        Editable && selection.Any(x => Snapshot.Find(x) is DiagramBox { Locked: false } or DiagramLink { Locked: false });

    private List<DiagramElement> Lockable(IEnumerable<string> selection) =>
        [.. selection.Select(Snapshot.Find).Where(static x => x is DiagramBox or DiagramLink).Cast<DiagramElement>()];

    private static bool IsLocked(DiagramElement element) => element is DiagramBox { Locked: true } or DiagramLink { Locked: true };

    private string? FirstEditableSlot(IReadOnlyList<string> selection)
    {
        if ((selection.Count != 1) || (Snapshot.Find(selection[0]) is not DiagramBox box) ||
            ((ScriptTables.PermissionsOf(box, Diagram.Index) & DiagramPermissions.EditText) == 0))
        {
            return null;
        }

        return LabelLayout.SlotsOf(box, Diagram.Index.FindBoxType(box)).FirstOrDefault(static x => x.Editable)?.Name;
    }

    private async Task SyncCommandsAsync(IJSObjectReference current)
    {
        if (subscribed is null)
        {
            return;
        }

        Commands.Refresh();
        var map = JsonSerializer.Serialize(
            Commands.All.Where(static x => x.Keys.Count > 0).Select(static x => new ScriptCommand { Name = x.Name, Keys = [.. x.Keys], Enabled = x.IsEnabled, Local = x.RunsLocally }).ToList(),
            ScriptJsonContext.Default.ListScriptCommand);
        if (!string.Equals(map, sentCommands, StringComparison.Ordinal))
        {
            sentCommands = map;
            await current.InvokeVoidAsync("setCommands", JsonDocument.Parse(map).RootElement).ConfigureAwait(true);
        }

        var palette = Diagram.Index.Schema.Rules.InPalette is null
            ? null
            : JsonSerializer.Serialize(TypeSearch.PaletteTypes(Snapshot, Diagram.Index).Select(static x => x.Name).ToList(), ScriptJsonContext.Default.IReadOnlyListString);
        if (!string.Equals(palette, sentPalette, StringComparison.Ordinal))
        {
            sentPalette = palette;
            await current.InvokeVoidAsync("setPaletteTypes", palette is null ? null : JsonDocument.Parse(palette).RootElement).ConfigureAwait(true);
        }
    }

    private async Task RefreshCommandsAsync()
    {
        if (controller is null)
        {
            Commands.Refresh();
            return;
        }

        try
        {
            await SyncCommandsAsync(controller).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async Task WriteAsync(Action write)
    {
        await commitLock.WaitAsync().ConfigureAwait(true);
        try
        {
            write();
        }
        finally
        {
            commitLock.Release();
        }
    }

    private async Task<bool> RunCommandAsync(DiagramOperation operation, Func<DiagramReason?> check, Func<Task<DiagramReason?>>? before, Action write, Func<DiagramSnapshot, Task>? after)
    {
        await commitLock.WaitAsync().ConfigureAwait(true);
        DiagramReason? reason;
        try
        {
            reason = check();
            if ((reason is null) && (before is not null))
            {
                var snapshot = Snapshot;
                reason = await before().ConfigureAwait(true);
                if ((reason is null) && !ReferenceEquals(snapshot, Snapshot))
                {
                    reason = check();
                }
            }

            if (reason is null)
            {
                write();
                if (after is not null)
                {
                    await after(Snapshot).ConfigureAwait(true);
                }

                return true;
            }
        }
        finally
        {
            commitLock.Release();
        }

        await CommitRejected.InvokeAsync(new DiagramCommitRejectedEventArgs(operation, reason)).ConfigureAwait(true);
        await RunViewAsync(current => current.InvokeVoidAsync("showReason", reason.Message ?? BuiltInTexts.Reason(reason.Kind) ?? string.Empty)).ConfigureAwait(true);
        return false;
    }

    private async Task GroupAsync(GroupingResult result)
    {
        if ((result.Edit is null) && (result.Reason is null))
        {
            return;
        }

        if (await RunCommandAsync(DiagramOperation.Command, () => result.Reason, null, () => Diagram.Write(null, DiagramChangeOrigin.User, result.Edit!), null).ConfigureAwait(true))
        {
            await SelectAsync(result.Selection).ConfigureAwait(true);
        }
    }

    private Task<bool> ReorderAsync(IEnumerable<string> selection, OrderMove move)
    {
        string[] ids = [.. selection.Where(x => Snapshot.Find(x) is DiagramBox { Locked: false } or DiagramLink { Locked: false })];
        return RunCommandAsync(DiagramOperation.Command, static () => null, null, () => Diagram.Write(null, DiagramChangeOrigin.User, editor => editor.Reorder(ids, move)), null);
    }

    private Task ArrangeAsync(List<(string Id, DiagramPoint Offset)> moves)
    {
        if (moves.Count == 0)
        {
            return Task.CompletedTask;
        }

        return RunCommandAsync(
            DiagramOperation.Command,
            static () => null,
            null,
            () => Diagram.Write(null, DiagramChangeOrigin.User, editor =>
            {
                foreach (var (id, offset) in moves)
                {
                    editor.Move([id], offset);
                }
            }),
            null);
    }

    private Task<bool> ToggleLockAsync(IEnumerable<string> selection)
    {
        var items = Lockable(selection);
        var locking = !items.All(IsLocked);
        return RunCommandAsync(
            DiagramOperation.Command,
            static () => null,
            null,
            () => Diagram.Write(null, DiagramChangeOrigin.User, editor =>
            {
                foreach (var item in items)
                {
                    editor.Replace(item switch
                    {
                        DiagramBox box => box with { Locked = locking },
                        DiagramLink link => link with { Locked = locking },
                        _ => item
                    });
                }
            }),
            null);
    }
}
