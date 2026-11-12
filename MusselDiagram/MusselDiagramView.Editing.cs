namespace MusselDiagram;

using System.Collections.Immutable;

using Microsoft.AspNetCore.Components;

using MusselDiagram.Internal;

public sealed partial class MusselDiagramView
{
    private ClipboardContent? clipboard;

    private int pastes;

    [Parameter]
    public bool DropEnabled { get; set; } = true;

    [Parameter]
    public bool TextEditEnabled { get; set; } = true;

    [Parameter]
    public bool TooltipEnabled { get; set; } = true;

    [Parameter]
    public Func<string, string>? SearchNormalizer { get; set; }

    [Parameter]
    public EventCallback<DiagramDroppingEventArgs> Dropping { get; set; }

    [Parameter]
    public EventCallback<DiagramDroppedEventArgs> Dropped { get; set; }

    [Parameter]
    public EventCallback<DiagramDeletingEventArgs> Deleting { get; set; }

    [Parameter]
    public EventCallback<DiagramDeletedEventArgs> Deleted { get; set; }

    [Parameter]
    public EventCallback<DiagramPastingEventArgs> Pasting { get; set; }

    [Parameter]
    public EventCallback<DiagramPastedEventArgs> Pasted { get; set; }

    [Parameter]
    public EventCallback<DiagramTextCommittingEventArgs> TextCommitting { get; set; }

    [Parameter]
    public EventCallback<DiagramTextCommittedEventArgs> TextCommitted { get; set; }

    [Parameter]
    public EventCallback<DiagramElementPointerEventArgs> ElementClicked { get; set; }

    [Parameter]
    public EventCallback<DiagramElementPointerEventArgs> ElementDoubleClicked { get; set; }

    [Parameter]
    public EventCallback<DiagramPointerEventArgs> BackgroundClicked { get; set; }

    [Parameter]
    public EventCallback<DiagramPointerEventArgs> BackgroundDoubleClicked { get; set; }

    internal Task<ScriptCommitResult> CommitDropAsync(string type, DiagramRect bounds, string? parentId, string? value, string? splitLinkId, long baseVersion, int rejectionsSeen, IReadOnlyList<string>? selection = null)
    {
        var element = NewBox(type, bounds, parentId, value);
        Splice? split = null;
        return CommitAsync(
            DiagramOperation.Drop,
            rejectionsSeen,
            selection,
            () =>
            {
                var reason = CheckDrop(element, baseVersion);
                split = (reason is null) && (element is DiagramNode) ? SpliceOf(element.Id, splitLinkId, baseVersion, editor => editor.Add(element)) : null;
                return reason;
            },
            async () =>
            {
                var args = new DiagramDroppingEventArgs(element!.Id, type, bounds, parentId, value, split?.Link.Id);
                await Dropping.InvokeAsync(args).ConfigureAwait(true);
                return Canceled(args.Cancel, args.Reason);
            },
            () =>
            {
                using (Diagram.BeginUpdate(null, DiagramChangeOrigin.User))
                {
                    Diagram.Add(element!);
                    if (split is { } splice)
                    {
                        ApplySplice(splice);
                    }
                }
            },
            snapshot => Dropped.InvokeAsync(new DiagramDroppedEventArgs(element!.Id, type, bounds, parentId, value, split?.Link.Id, snapshot)),
            element?.Id);
    }

    internal Task<ScriptCommitResult> CommitTextAsync(string elementId, string slot, string text, long baseVersion, int rejectionsSeen, IReadOnlyList<string>? selection = null)
    {
        var newText = text.Length == 0 ? null : text;
        string? oldText = null;
        return CommitAsync(
            DiagramOperation.TextEdit,
            rejectionsSeen,
            selection,
            () =>
            {
                var reason = CheckText(elementId, slot, baseVersion);
                oldText = reason is null ? TextOf((DiagramBox)Snapshot.Find(elementId)!, slot) : null;
                return reason;
            },
            async () =>
            {
                if (string.Equals(oldText, newText, StringComparison.Ordinal))
                {
                    return null;
                }

                var args = new DiagramTextCommittingEventArgs(elementId, slot, oldText, newText);
                await TextCommitting.InvokeAsync(args).ConfigureAwait(true);
                return Canceled(args.Cancel, args.Reason);
            },
            () =>
            {
                if (!string.Equals(oldText, newText, StringComparison.Ordinal) && (Snapshot.Find(elementId) is DiagramBox box))
                {
                    Diagram.Write(null, DiagramChangeOrigin.User, editor => editor.Replace(WithText(box, slot, newText)));
                }
            },
            snapshot => string.Equals(oldText, newText, StringComparison.Ordinal)
                ? Task.CompletedTask
                : TextCommitted.InvokeAsync(new DiagramTextCommittedEventArgs(elementId, slot, oldText, newText, snapshot)));
    }

    internal Task OnClickedAsync(string? id, DiagramPoint position, bool shift, bool ctrl, bool alt, bool meta, bool twice) =>
        (id, twice) switch
        {
            (null, false) => BackgroundClicked.InvokeAsync(new DiagramPointerEventArgs(position, shift, ctrl, alt, meta)),
            (null, true) => BackgroundDoubleClicked.InvokeAsync(new DiagramPointerEventArgs(position, shift, ctrl, alt, meta)),
            (_, false) => ElementClicked.InvokeAsync(new DiagramElementPointerEventArgs(id, position, shift, ctrl, alt, meta)),
            _ => ElementDoubleClicked.InvokeAsync(new DiagramElementPointerEventArgs(id, position, shift, ctrl, alt, meta))
        };

    internal IReadOnlyList<string> SearchTypes(string query, IEnumerable<string> names)
    {
        var index = Diagram.Index;
        var types = names.Select(index.FindBoxType).OfType<DiagramBoxType>().ToList();
        return [.. TypeSearch.Search(types, query, index, SearchNormalizer).Select(static x => x.Name)];
    }

    private string? TextOf(DiagramBox box, string slot) =>
        string.Equals(LabelLayout.SlotsOf(box, Diagram.Index.FindBoxType(box))[0].Name, slot, StringComparison.Ordinal) ? box.Label : box.Texts.GetValueOrDefault(slot);

    private DiagramBox WithText(DiagramBox box, string slot, string? text)
    {
        var slots = LabelLayout.SlotsOf(box, Diagram.Index.FindBoxType(box));
        if (string.Equals(slots[0].Name, slot, StringComparison.Ordinal))
        {
            return box with { Label = text };
        }

        return box with { Texts = text is null ? box.Texts.Remove(slot) : box.Texts.SetItem(slot, text) };
    }

    private DiagramReason? CheckText(string elementId, string slot, long baseVersion)
    {
        if (ReadOnly || !TextEditEnabled)
        {
            return Reason(DiagramReasonKind.Permission);
        }

        if ((SnapshotAt(baseVersion) is not { } origin) || (origin.Find(elementId) is not DiagramBox old) || (Snapshot.Find(elementId) is not DiagramBox box))
        {
            return Reason(DiagramReasonKind.Changed);
        }

        var type = Diagram.Index.FindBoxType(box);
        var slots = LabelLayout.SlotsOf(box, type);
        var position = -1;
        for (var i = 0; i < slots.Length; i++)
        {
            if (string.Equals(slots[i].Name, slot, StringComparison.Ordinal))
            {
                position = i;
                break;
            }
        }

        if ((position < 0) || !slots[position].Editable || ((ScriptTables.PermissionsOf(box, Diagram.Index) & DiagramPermissions.EditText) == 0))
        {
            return Reason(DiagramReasonKind.Permission);
        }

        var before = position == 0 ? old.Label : old.Texts.GetValueOrDefault(slot);
        var now = position == 0 ? box.Label : box.Texts.GetValueOrDefault(slot);
        return string.Equals(before, now, StringComparison.Ordinal) ? null : Reason(DiagramReasonKind.Changed);
    }

    private DiagramReason? CheckDrop(DiagramBox? element, long baseVersion)
    {
        if (ReadOnly || !DropEnabled || (element is null))
        {
            return Reason(DiagramReasonKind.Permission);
        }

        if (SnapshotAt(baseVersion) is null)
        {
            return Reason(DiagramReasonKind.Changed);
        }

        var current = Snapshot;
        var index = Diagram.Index;
        var type = index.FindBoxType(element)!;
        if ((type.MaxCount is { } max) && (current.Groups.Cast<DiagramBox>().Concat(current.Nodes).Count(x => ReferenceEquals(index.FindBoxType(x), type)) >= max))
        {
            return Reason(DiagramReasonKind.Count);
        }

        if (!CanBeParent(element.ParentId))
        {
            return Reason(DiagramReasonKind.Nesting);
        }

        return DiagramRules.CheckNesting(current, index, null, element.Type, element.ParentId);
    }

    private Splice? SpliceOf(string nodeId, string? splitLinkId, long baseVersion, Action<SnapshotEditor> change)
    {
        if ((splitLinkId is null) ||
            (SnapshotAt(baseVersion) is not { } origin) || (origin.Find(splitLinkId) is not DiagramLink old) ||
            (Snapshot.Find(splitLinkId) is not DiagramLink link) || (old.Source != link.Source) || (old.Target != link.Target) ||
            string.Equals(link.Source.ElementId, nodeId, StringComparison.Ordinal) || string.Equals(link.Target.ElementId, nodeId, StringComparison.Ordinal))
        {
            return null;
        }

        var index = Diagram.Index;
        DiagramSnapshot trial;
        try
        {
            var editor = new SnapshotEditor(Snapshot, index) { SkipRoutes = true };
            change(editor);
            editor.Complete();
            trial = editor.Build(Snapshot.Version);
        }
        catch (DiagramException)
        {
            return null;
        }

        if (trial.Find(nodeId) is not DiagramNode node)
        {
            return null;
        }

        var candidates = index.PortsOf(node).Select(x => new DiagramLinkEnd(node.Id, x.Id)).ToList();
        if ((index.FindBoxType(node) is { ConnectsAsWhole: true }) || (candidates.Count == 0))
        {
            candidates.Add(new DiagramLinkEnd(node.Id));
        }

        bool Others(DiagramLink x) => !string.Equals(x.Id, link.Id, StringComparison.Ordinal);
        var incoming = candidates.Where(x => DiagramRules.CheckConnection(trial, index, link.Source, x, link.Type, Others, link.Id, true) is null).Select(static x => (DiagramLinkEnd?)x).FirstOrDefault();
        var outgoing = candidates.Where(x => DiagramRules.CheckConnection(trial, index, x, link.Target, link.Type, Others, null, true) is null).Select(static x => (DiagramLinkEnd?)x).FirstOrDefault();
        return (incoming is { } into) && (outgoing is { } outOf) ? new Splice(link, into, outOf, DiagramIds.Create()) : null;
    }

    private void ApplySplice(Splice splice)
    {
        Diagram.Reconnect(splice.Link.Id, DiagramLinkEndSide.Target, splice.In, null, DiagramChangeOrigin.User);
        Diagram.Connect(NewLink(splice.NewLinkId, splice.Out, splice.Link.Target, splice.Link.Type));
    }

    private Task CopyAsync(DiagramCommandContext context)
    {
        clipboard = Clipboard.Copy(Snapshot, Diagram.Index, context.SelectedIds);
        pastes = 0;
        return RefreshCommandsAsync();
    }

    private async Task CutAsync(DiagramCommandContext context)
    {
        await CopyAsync(context).ConfigureAwait(true);
        await DeleteAsync(context.SelectedIds, true).ConfigureAwait(true);
    }

    private Task DeleteAsync(IEnumerable<string> selection, bool isCut)
    {
        var index = Diagram.Index;
        string[] selected = [.. selection];
        var ids = Deletion.Deletable(Snapshot, index, selected);
        if (ids.Count == 0)
        {
            return Task.CompletedTask;
        }

        ImmutableArray<string> removed = [];
        return RunCommandAsync(
            DiagramOperation.Delete,
            () =>
            {
                ids = Deletion.Deletable(Snapshot, index, selected);
                if (ids.Count == 0)
                {
                    return Reason(DiagramReasonKind.Changed);
                }

                removed = Deletion.Removed(Snapshot, index, ids);
                if (index.Schema.Rules.CanDelete?.Invoke(new DiagramDeletionContext { Snapshot = Snapshot, ElementIds = removed }) is { Allowed: false } result)
                {
                    return new DiagramReason(DiagramReasonKind.App, result.Reason);
                }

                return null;
            },
            async () =>
            {
                var args = new DiagramDeletingEventArgs(removed, isCut);
                await Deleting.InvokeAsync(args).ConfigureAwait(true);
                return Canceled(args.Cancel, args.Reason);
            },
            () => Diagram.Write(null, DiagramChangeOrigin.User, editor => editor.Delete(ids)),
            snapshot => Deleted.InvokeAsync(new DiagramDeletedEventArgs(removed, isCut, snapshot)));
    }

    private async Task PasteAsync()
    {
        if (clipboard is not { } content)
        {
            return;
        }

        pastes++;
        var step = Diagram.Schema.GridSize * 2 * pastes;
        PasteResult? pasted = null;
        var accepted = await RunCommandAsync(
            DiagramOperation.Paste,
            () =>
            {
                var (result, reason) = Clipboard.Paste(Snapshot, Diagram.Index, content, new DiagramPoint(step, step), pasted?.IdMap);
                pasted = result;
                return reason;
            },
            async () =>
            {
                var args = new DiagramPastingEventArgs(pasted!.Elements, pasted.IdMap);
                await Pasting.InvokeAsync(args).ConfigureAwait(true);
                return Canceled(args.Cancel, args.Reason);
            },
            () => Diagram.Write(null, DiagramChangeOrigin.User, editor =>
            {
                foreach (var element in pasted!.Elements)
                {
                    editor.Add(element);
                }
            }),
            snapshot => Pasted.InvokeAsync(new DiagramPastedEventArgs(pasted!.Elements, pasted.IdMap, snapshot))).ConfigureAwait(true);
        if (accepted)
        {
            await SelectAsync(pasted!.Selection).ConfigureAwait(true);
        }
    }

    private sealed record Splice(DiagramLink Link, DiagramLinkEnd In, DiagramLinkEnd Out, string NewLinkId);
}
