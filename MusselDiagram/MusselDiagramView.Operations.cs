namespace MusselDiagram;

using Microsoft.AspNetCore.Components;

using MusselDiagram.Internal;

public sealed partial class MusselDiagramView
{
    [Parameter]
    public bool BoxSelectionEnabled { get; set; } = true;

    [Parameter]
    public bool ResizeEnabled { get; set; } = true;

    [Parameter]
    public bool ConnectEnabled { get; set; } = true;

    [Parameter]
    public string? LinkType { get; set; }

    [Parameter]
    public bool GridVisible { get; set; }

    [Parameter]
    public DiagramGridStyle GridStyle { get; set; }

    [Parameter]
    public double GridSize { get; set; } = 10;

    [Parameter]
    public bool SnapToGrid { get; set; } = true;

    [Parameter]
    public bool SnapToElements { get; set; } = true;

    [Parameter]
    public double SnapTolerance { get; set; } = 8;

    [Parameter]
    public double ConnectionRadius { get; set; } = 20;

    [Parameter]
    public double QuickAddSpacing { get; set; } = 80;

    [Parameter]
    public EventCallback<DiagramResizingEventArgs> Resizing { get; set; }

    [Parameter]
    public EventCallback<DiagramResizedEventArgs> Resized { get; set; }

    [Parameter]
    public EventCallback<DiagramConnectingEventArgs> Connecting { get; set; }

    [Parameter]
    public EventCallback<DiagramConnectedEventArgs> Connected { get; set; }

    [Parameter]
    public EventCallback<DiagramReconnectingEventArgs> Reconnecting { get; set; }

    [Parameter]
    public EventCallback<DiagramReconnectedEventArgs> Reconnected { get; set; }

    internal Task<ScriptCommitResult> CommitMoveAsync(string[] ids, DiagramPoint offset, bool reparent, string? parentId, string? splitLinkId, long baseVersion, int rejectionsSeen)
    {
        Splice? split = null;
        return CommitAsync(
            DiagramOperation.Move,
            rejectionsSeen,
            () =>
            {
                var reason = CheckMove(ids, baseVersion) ?? (reparent ? CheckParent(ids, parentId) : null);
                split = (reason is null) && (ids.Length == 1) && (Snapshot.GetLinks(ids[0]).Length == 0)
                    ? SpliceOf(ids[0], splitLinkId, baseVersion, editor =>
                    {
                        editor.Move(ids, offset);
                        if (reparent)
                        {
                            editor.SetParent(ids, parentId);
                        }
                    })
                    : null;
                return reason;
            },
            async () =>
            {
                var args = new DiagramMovingEventArgs(ids, offset, reparent, reparent ? parentId : null);
                await Moving.InvokeAsync(args).ConfigureAwait(true);
                return Canceled(args.Cancel, args.Reason);
            },
            () =>
            {
                using (Diagram.BeginUpdate(null, DiagramChangeOrigin.User))
                {
                    Diagram.Move(ids, offset);
                    if (reparent)
                    {
                        Diagram.SetParent(ids, parentId);
                    }

                    if (split is { } splice)
                    {
                        ApplySplice(splice);
                    }
                }
            },
            snapshot => Moved.InvokeAsync(new DiagramMovedEventArgs(ids, offset, reparent, reparent ? parentId : null, snapshot)));
    }

    internal Task<ScriptCommitResult> CommitResizeAsync(string id, DiagramRect bounds, long baseVersion, int rejectionsSeen) =>
        CommitAsync(
            DiagramOperation.Resize,
            rejectionsSeen,
            () => CheckResize(id, bounds, baseVersion),
            async () =>
            {
                var args = new DiagramResizingEventArgs(id, bounds);
                await Resizing.InvokeAsync(args).ConfigureAwait(true);
                return Canceled(args.Cancel, args.Reason);
            },
            () => Diagram.Resize(id, bounds, null, DiagramChangeOrigin.User),
            snapshot => Resized.InvokeAsync(new DiagramResizedEventArgs(id, bounds, snapshot)));

    internal Task<ScriptCommitResult> CommitConnectAsync(DiagramLinkEnd source, DiagramLinkEnd target, string linkType, long baseVersion, int rejectionsSeen)
    {
        var linkId = DiagramIds.Create();
        return CommitAsync(
            DiagramOperation.Connect,
            rejectionsSeen,
            () => CheckConnect(source, target, linkType, baseVersion),
            async () =>
            {
                var args = new DiagramConnectingEventArgs(source, target, linkType, null);
                await Connecting.InvokeAsync(args).ConfigureAwait(true);
                return Canceled(args.Cancel, args.Reason);
            },
            () => Diagram.Write(null, DiagramChangeOrigin.User, editor => editor.Add(NewLink(linkId, source, target, linkType))),
            snapshot => Connected.InvokeAsync(new DiagramConnectedEventArgs(linkId, source, target, linkType, null, snapshot)));
    }

    internal Task<ScriptCommitResult> CommitReconnectAsync(string linkId, DiagramLinkEndSide side, DiagramLinkEnd end, long baseVersion, int rejectionsSeen) =>
        CommitAsync(
            DiagramOperation.Reconnect,
            rejectionsSeen,
            () => CheckReconnect(linkId, side, end, baseVersion),
            async () =>
            {
                var args = new DiagramReconnectingEventArgs(linkId, side, end);
                await Reconnecting.InvokeAsync(args).ConfigureAwait(true);
                return Canceled(args.Cancel, args.Reason);
            },
            () => Diagram.Reconnect(linkId, side, end, null, DiagramChangeOrigin.User),
            snapshot => Reconnected.InvokeAsync(new DiagramReconnectedEventArgs(linkId, side, end, snapshot)));

    internal Task<ScriptCommitResult> CommitPlaceAsync(string type, DiagramRect bounds, string? parentId, DiagramLinkEnd from, string linkType, long baseVersion, int rejectionsSeen)
    {
        var element = NewBox(type, bounds, parentId);
        var linkId = DiagramIds.Create();
        var target = default(DiagramLinkEnd);
        return CommitAsync(
            DiagramOperation.Connect,
            rejectionsSeen,
            () => CheckPlace(element, from, linkType, baseVersion, out target),
            async () =>
            {
                var args = new DiagramConnectingEventArgs(from, target, linkType, element);
                await Connecting.InvokeAsync(args).ConfigureAwait(true);
                return Canceled(args.Cancel, args.Reason);
            },
            () =>
            {
                using (Diagram.BeginUpdate(null, DiagramChangeOrigin.User))
                {
                    Diagram.Add(element!);
                    Diagram.Connect(NewLink(linkId, from, target, linkType));
                }
            },
            snapshot => Connected.InvokeAsync(new DiagramConnectedEventArgs(linkId, from, target, linkType, element, snapshot)));
    }

    private static DiagramReason? Canceled(bool cancel, string? reason) =>
        cancel ? new DiagramReason(reason is null ? DiagramReasonKind.Canceled : DiagramReasonKind.App, reason) : null;

    private static DiagramReason Reason(DiagramReasonKind kind) => new(kind, DiagramCultures.Reason(kind));

    private async Task<ScriptCommitResult> CommitAsync(
        DiagramOperation operation,
        int rejectionsSeen,
        Func<DiagramReason?> check,
        Func<Task<DiagramReason?>> before,
        Action write,
        Func<DiagramSnapshot, Task> after,
        string? elementId = null)
    {
        await commitLock.WaitAsync().ConfigureAwait(true);
        try
        {
            if (rejectionsSeen < rejections)
            {
                return new ScriptCommitResult(false, Snapshot.Version, null, rejections);
            }

            var reason = check();
            if (reason is null)
            {
                var snapshot = Snapshot;
                reason = await before().ConfigureAwait(true);
                if ((reason is null) && !ReferenceEquals(snapshot, Snapshot))
                {
                    reason = check();
                }
            }

            if (reason is not null)
            {
                rejections++;
                await CommitRejected.InvokeAsync(new DiagramCommitRejectedEventArgs(operation, reason)).ConfigureAwait(true);
                return new ScriptCommitResult(false, Snapshot.Version, reason.Message ?? DiagramCultures.Reason(reason.Kind), rejections);
            }

            write();
            var result = Snapshot;
            await after(result).ConfigureAwait(true);
            await RefreshCommandsAsync().ConfigureAwait(true);
            return new ScriptCommitResult(true, result.Version, null, rejections, elementId);
        }
        finally
        {
            commitLock.Release();
        }
    }

    private DiagramLink NewLink(string id, DiagramLinkEnd source, DiagramLinkEnd target, string linkType) =>
        new() { Id = id, Type = linkType, Source = source, Target = target, Data = Diagram.Index.FindLinkType(linkType)?.DefaultData };

    private DiagramBox? NewBox(string type, DiagramRect bounds, string? parentId, string? value = null)
    {
        var boxType = Diagram.Index.FindBoxType(type);
        if (boxType is not (DiagramNodeType or DiagramGroupType { Kind: DiagramGroupKind.Tree }))
        {
            return null;
        }

        var content = boxType.CreateContent?.Invoke(new DiagramCreateContext { Snapshot = Snapshot, Type = boxType, Value = value });
        var data = content is null ? boxType.DefaultData : content.Data;
        return boxType is DiagramNodeType
            ? new DiagramNode { Type = boxType.Name, Bounds = bounds, ParentId = parentId, Label = content?.Label, Texts = content?.Texts ?? [], Data = data }
            : new DiagramGroup { Type = boxType.Name, Bounds = bounds, ParentId = parentId, Label = content?.Label, Texts = content?.Texts ?? [], Data = data };
    }

    private DiagramSnapshot? SnapshotAt(long version) =>
        version == Snapshot.Version ? Snapshot : history.GetValueOrDefault(version);

    private DiagramReason? CheckMove(IEnumerable<string> ids, long baseVersion)
    {
        if (ReadOnly || !MoveEnabled)
        {
            return Reason(DiagramReasonKind.Permission);
        }

        var current = Snapshot;
        if (SnapshotAt(baseVersion) is not { } origin)
        {
            return Reason(DiagramReasonKind.Changed);
        }

        foreach (var id in ids)
        {
            if ((current.Find(id) is not DiagramBox box) || (origin.Find(id) is not DiagramBox))
            {
                return Reason(DiagramReasonKind.Changed);
            }

            if (!ScriptTables.CanMove(box, Diagram.Index))
            {
                return Reason(DiagramReasonKind.Permission);
            }

            var descendants = current.GetDescendants(id);
            if (descendants.Length != origin.GetDescendants(id).Length)
            {
                return Reason(DiagramReasonKind.Changed);
            }

            foreach (var moved in descendants.Prepend(box))
            {
                if ((origin.Find(moved.Id) is not DiagramBox old) || (old.Bounds != moved.Bounds) || !string.Equals(old.ParentId, moved.ParentId, StringComparison.Ordinal))
                {
                    return Reason(DiagramReasonKind.Changed);
                }
            }
        }

        return null;
    }

    private DiagramReason? CheckParent(IEnumerable<string> ids, string? parentId)
    {
        var current = Snapshot;
        var index = Diagram.Index;
        if ((parentId is not null) && (current.Find(parentId) is not DiagramGroup { Kind: DiagramGroupKind.Tree, Locked: false }))
        {
            return Reason(DiagramReasonKind.Nesting);
        }

        foreach (var id in ids)
        {
            if (current.Find(id) is not DiagramBox box)
            {
                return Reason(DiagramReasonKind.Changed);
            }

            if (string.Equals(box.ParentId, parentId, StringComparison.Ordinal))
            {
                continue;
            }

            if ((box is DiagramGroup { Kind: DiagramGroupKind.Overlay }) ||
                (index.FindBoxType(box) is not { StayInParent: false }) ||
                ((parentId is not null) && (string.Equals(parentId, id, StringComparison.Ordinal) || current.GetAncestors(parentId).Any(x => string.Equals(x.Id, id, StringComparison.Ordinal)))))
            {
                return Reason(DiagramReasonKind.Nesting);
            }

            if (DiagramRules.CheckNesting(current, index, id, box.Type, parentId) is { } reason)
            {
                return reason;
            }
        }

        return null;
    }

    private DiagramReason? CheckResize(string id, DiagramRect bounds, long baseVersion)
    {
        if (ReadOnly || !ResizeEnabled)
        {
            return Reason(DiagramReasonKind.Permission);
        }

        if ((SnapshotAt(baseVersion) is not { } origin) || (Snapshot.Find(id) is not DiagramBox box) || (origin.Find(id) is not DiagramBox old) || (old.Bounds != box.Bounds))
        {
            return Reason(DiagramReasonKind.Changed);
        }

        var minimum = Diagram.Index.FindBoxType(box)?.MinSize;
        if (!ScriptTables.CanResize(box, Diagram.Index) ||
            !double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) ||
            (bounds.Width <= 0) || (bounds.Height <= 0) ||
            ((minimum is { } min) && ((bounds.Width < min.Width - 0.001) || (bounds.Height < min.Height - 0.001))))
        {
            return Reason(DiagramReasonKind.Permission);
        }

        return null;
    }

    private DiagramReason? CheckConnect(DiagramLinkEnd source, DiagramLinkEnd target, string linkType, long baseVersion)
    {
        if (ReadOnly || !ConnectEnabled)
        {
            return Reason(DiagramReasonKind.Permission);
        }

        if ((SnapshotAt(baseVersion) is not { } origin) || !origin.Contains(source.ElementId) || !origin.Contains(target.ElementId))
        {
            return Reason(DiagramReasonKind.Changed);
        }

        return DiagramRules.CheckConnection(Snapshot, Diagram.Index, source, target, linkType, static _ => true, null, true);
    }

    private DiagramReason? CheckReconnect(string linkId, DiagramLinkEndSide side, DiagramLinkEnd end, long baseVersion)
    {
        if (ReadOnly || !ConnectEnabled)
        {
            return Reason(DiagramReasonKind.Permission);
        }

        if ((SnapshotAt(baseVersion) is not { } origin) ||
            (Snapshot.Find(linkId) is not DiagramLink link) ||
            (origin.Find(linkId) is not DiagramLink old) ||
            (old.Source != link.Source) || (old.Target != link.Target) ||
            !origin.Contains(end.ElementId))
        {
            return Reason(DiagramReasonKind.Changed);
        }

        if ((ScriptTables.PermissionsOf(link, Diagram.Index) & DiagramPermissions.Connect) == 0)
        {
            return Reason(DiagramReasonKind.Permission);
        }

        var source = side == DiagramLinkEndSide.Source ? end : link.Source;
        var target = side == DiagramLinkEndSide.Target ? end : link.Target;
        return DiagramRules.CheckConnection(Snapshot, Diagram.Index, source, target, link.Type, x => !string.Equals(x.Id, linkId, StringComparison.Ordinal), linkId, true);
    }

    private DiagramReason? CheckPlace(DiagramBox? element, DiagramLinkEnd from, string linkType, long baseVersion, out DiagramLinkEnd target)
    {
        target = default;
        if (ReadOnly || !ConnectEnabled || (element is null))
        {
            return Reason(DiagramReasonKind.Permission);
        }

        var current = Snapshot;
        var index = Diagram.Index;
        if ((SnapshotAt(baseVersion) is not { } origin) || !origin.Contains(from.ElementId) || !current.Contains(from.ElementId))
        {
            return Reason(DiagramReasonKind.Changed);
        }

        var type = index.FindBoxType(element)!;
        if ((type.MaxCount is { } max) && (current.Groups.Cast<DiagramBox>().Concat(current.Nodes).Count(x => ReferenceEquals(index.FindBoxType(x), type)) >= max))
        {
            return Reason(DiagramReasonKind.Count);
        }

        if ((element.ParentId is not null) && (current.Find(element.ParentId) is not DiagramGroup { Locked: false }))
        {
            return Reason(DiagramReasonKind.Nesting);
        }

        if (DiagramRules.CheckNesting(current, index, null, element.Type, element.ParentId) is { } nesting)
        {
            return nesting;
        }

        DiagramSnapshot trial;
        try
        {
            var editor = new SnapshotEditor(current, index) { SkipRoutes = true };
            editor.Add(element);
            editor.Complete();
            trial = editor.Build(current.Version);
        }
        catch (DiagramException)
        {
            return Reason(DiagramReasonKind.Nesting);
        }

        var placed = (DiagramBox)trial.Find(element.Id)!;
        var candidates = index.PortsOf(placed).Select(x => new DiagramLinkEnd(placed.Id, x.Id)).ToList();
        if (type.ConnectsAsWhole || (candidates.Count == 0))
        {
            candidates.Add(new DiagramLinkEnd(placed.Id));
        }

        DiagramReason? first = null;
        foreach (var candidate in candidates)
        {
            var reason = DiagramRules.CheckConnection(trial, index, from, candidate, linkType, static _ => true, null, true);
            if (reason is null)
            {
                target = candidate;
                return null;
            }

            first ??= reason;
        }

        return first ?? Reason(DiagramReasonKind.NoConnectionPoint);
    }
}
