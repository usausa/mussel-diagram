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
    public int GridMajorInterval { get; set; } = 4;

    [Parameter]
    public double GridMajorLineWidth { get; set; } = 1;

    [Parameter]
    public bool SnapToGrid { get; set; } = true;

    [Parameter]
    public bool SnapToElements { get; set; } = true;

    [Parameter]
    public double SnapTolerance { get; set; } = 8;

    [Parameter]
    public double ConnectionRadius { get; set; } = 20;

    [Parameter]
    public double LinkHitWidth { get; set; } = 10;

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

    internal Task<ScriptCommitResult> CommitMoveAsync(string[] ids, DiagramPoint offset, bool reparent, string? parentId, string? splitLinkId, long startVersion, int rejectionsSeen, IReadOnlyList<string>? selection = null)
    {
        Splice? split = null;
        return CommitAsync(
            DiagramOperation.Move,
            rejectionsSeen,
            selection,
            () =>
            {
                var reason = CheckMove(ids, offset, startVersion) ?? (reparent ? CheckParent(ids, parentId) : null) ??
                    (StayRules.Allows(Snapshot, Diagram.Index, ids, offset) ? null : Reason(DiagramReasonKind.Nesting));
                var splits = (reason is null) && (splitLinkId is not null);
                if (splits)
                {
                    reason = (ids.Length == 1) && (Snapshot.GetLinks(ids[0]).Length == 0) ? CheckSplit(splitLinkId, startVersion) : Reason(DiagramReasonKind.Changed);
                }

                split = splits && (reason is null)
                    ? SpliceOf(ids[0], splitLinkId, editor =>
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

    internal Task<ScriptCommitResult> CommitResizeAsync(string id, DiagramThickness edges, long startVersion, int rejectionsSeen, IReadOnlyList<string>? selection = null)
    {
        var bounds = default(DiagramRect);
        return CommitAsync(
            DiagramOperation.Resize,
            rejectionsSeen,
            selection,
            () =>
            {
                bounds = Snapshot.Find(id) is DiagramBox { Bounds: var current }
                    ? new DiagramRect(current.X + edges.Left, current.Y + edges.Top, current.Width - edges.Left + edges.Right, current.Height - edges.Top + edges.Bottom)
                    : default;
                return CheckResize(id, bounds, startVersion);
            },
            async () =>
            {
                var args = new DiagramResizingEventArgs(id, bounds);
                await Resizing.InvokeAsync(args).ConfigureAwait(true);
                return Canceled(args.Cancel, args.Reason);
            },
            () => Diagram.Resize(id, bounds, null, DiagramChangeOrigin.User),
            snapshot => Resized.InvokeAsync(new DiagramResizedEventArgs(id, bounds, snapshot)));
    }

    internal Task<ScriptCommitResult> CommitConnectAsync(DiagramLinkEnd source, DiagramLinkEnd target, string linkType, int rejectionsSeen, IReadOnlyList<string>? selection = null)
    {
        var linkId = DiagramIds.Create();
        return CommitAsync(
            DiagramOperation.Connect,
            rejectionsSeen,
            selection,
            () => CheckConnect(source, target, linkType),
            async () =>
            {
                var args = new DiagramConnectingEventArgs(source, target, linkType, null);
                await Connecting.InvokeAsync(args).ConfigureAwait(true);
                return Canceled(args.Cancel, args.Reason);
            },
            () => Diagram.Write(null, DiagramChangeOrigin.User, editor => editor.Add(NewLink(linkId, source, target, linkType))),
            snapshot => Connected.InvokeAsync(new DiagramConnectedEventArgs(linkId, source, target, linkType, null, snapshot)));
    }

    internal Task<ScriptCommitResult> CommitReconnectAsync(string linkId, DiagramLinkEndSide side, DiagramLinkEnd end, long startVersion, int rejectionsSeen, IReadOnlyList<string>? selection = null)
    {
        DiagramLink? link = null;
        return CommitAsync(
            DiagramOperation.Reconnect,
            rejectionsSeen,
            selection,
            () => CheckReconnect(linkId, side, end, startVersion, out link),
            async () =>
            {
                var args = new DiagramReconnectingEventArgs(linkId, side, end, EndOf(link!, side, end, DiagramLinkEndSide.Source), EndOf(link!, side, end, DiagramLinkEndSide.Target), link!.Type);
                await Reconnecting.InvokeAsync(args).ConfigureAwait(true);
                return Canceled(args.Cancel, args.Reason);
            },
            () => Diagram.Reconnect(linkId, side, end, null, DiagramChangeOrigin.User),
            snapshot => Reconnected.InvokeAsync(new DiagramReconnectedEventArgs(linkId, side, end, EndOf(link!, side, end, DiagramLinkEndSide.Source), EndOf(link!, side, end, DiagramLinkEndSide.Target), link!.Type, snapshot)));
    }

    internal async Task<ScriptCommitResult> CommitPlaceAsync(string type, DiagramRect bounds, string? parentId, DiagramLinkEnd from, string linkType, int rejectionsSeen, IReadOnlyList<string>? selection = null)
    {
        var element = NewBox(type, bounds, parentId);
        var linkId = DiagramIds.Create();
        var target = default(DiagramLinkEnd);
        return await CommitAsync(
            DiagramOperation.Connect,
            rejectionsSeen,
            selection,
            () => CheckPlace(element, from, linkType, out target),
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
            snapshot => Connected.InvokeAsync(new DiagramConnectedEventArgs(linkId, from, target, linkType, element, snapshot)),
            element?.Id).ConfigureAwait(true);
    }

    private static DiagramLinkEnd EndOf(DiagramLink link, DiagramLinkEndSide side, DiagramLinkEnd end, DiagramLinkEndSide wanted) =>
        side == wanted ? end : wanted == DiagramLinkEndSide.Source ? link.Source : link.Target;

    private static DiagramReason? Canceled(bool cancel, string? reason) =>
        cancel ? new DiagramReason(reason is null ? DiagramReasonKind.Canceled : DiagramReasonKind.App, reason) : null;

    private static DiagramReason Reason(DiagramReasonKind kind) => new(kind);

    private async Task<ScriptCommitResult> CommitAsync(
        DiagramOperation operation,
        int rejectionsSeen,
        IReadOnlyList<string>? selection,
        Func<DiagramReason?> check,
        Func<Task<DiagramReason?>> before,
        Action write,
        Func<DiagramSnapshot, Task> after,
        string? elementId = null)
    {
        if (selection is not null)
        {
            scriptSelection = selection;
        }

        if (scriptSelection is not null)
        {
            await SetSelectionAsync([.. scriptSelection.Where(IsSelectable)]).ConfigureAwait(true);
        }

        var turn = await EnterGateAsync().ConfigureAwait(true);
        turn?.Begin();
        try
        {
            if (IsDisposed)
            {
                throw new OperationCanceledException();
            }

            if (rejectionsSeen < rejections)
            {
                return new ScriptCommitResult(false, Snapshot.Version, null, rejections);
            }

            var reason = check();
            if (reason is null)
            {
                var snapshot = Snapshot;
                reason = await HandleAsync(turn, before).ConfigureAwait(true);
                if ((reason is null) && !ReferenceEquals(snapshot, Snapshot))
                {
                    reason = check();
                }
            }

            if (reason is not null)
            {
                rejections++;
                var localized = Options.Localize(reason);
                await HandleAsync(turn, () => CommitRejected.InvokeAsync(new DiagramCommitRejectedEventArgs(operation, localized))).ConfigureAwait(true);
                return new ScriptCommitResult(false, Snapshot.Version, localized.Message, rejections);
            }

            writing = true;
            try
            {
                write();
            }
            finally
            {
                writing = false;
            }

            var result = Snapshot;
            await HandleAsync(turn, () => after(result)).ConfigureAwait(true);
            await RefreshCommandsAsync().ConfigureAwait(true);
            return new ScriptCommitResult(true, result.Version, null, rejections, elementId);
        }
        finally
        {
            turn?.End();
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

    private DiagramReason? CheckMove(IEnumerable<string> ids, DiagramPoint offset, long startVersion)
    {
        if (ReadOnly || !MoveEnabled)
        {
            return Reason(DiagramReasonKind.Permission);
        }

        var current = Snapshot;
        foreach (var id in ids)
        {
            if (current.Find(id) is not DiagramBox box)
            {
                return Reason(DiagramReasonKind.Changed);
            }

            if (!ScriptTables.CanMove(box, Diagram.Index))
            {
                return Reason(DiagramReasonKind.Permission);
            }

            if (stamps.BoxChanged(id, startVersion))
            {
                return Reason(DiagramReasonKind.Changed);
            }

            foreach (var start in box is DiagramGroup { Kind: DiagramGroupKind.Overlay } overlay ? overlay.MemberIds : [id])
            {
                var descendants = current.GetDescendants(start);
                if (stamps.BoxChanged(start, startVersion) || descendants.Any(x => stamps.BoxChanged(x.Id, startVersion)))
                {
                    return Reason(DiagramReasonKind.Changed);
                }

                if (((current.Find(start) is DiagramBox moved) && !Fits(moved)) || !descendants.All(Fits))
                {
                    return Reason(DiagramReasonKind.Nesting);
                }
            }
        }

        return null;

        bool Fits(DiagramBox moved) => SnapshotEditor.IsInside(moved.Bounds.Offset(offset));
    }

    private DiagramReason? CheckParent(IEnumerable<string> ids, string? parentId)
    {
        var current = Snapshot;
        var index = Diagram.Index;
        if (!CanBeParent(parentId))
        {
            return Reason(DiagramReasonKind.Nesting);
        }

        foreach (var id in ids)
        {
            if (current.Find(id) is not DiagramBox box)
            {
                return Reason(DiagramReasonKind.Changed);
            }

            if (String.Equals(box.ParentId, parentId, StringComparison.Ordinal))
            {
                continue;
            }

            if ((box is DiagramGroup { Kind: DiagramGroupKind.Overlay }) ||
                (index.FindBoxType(box) is not { StayInParent: false }) ||
                ((parentId is not null) && (String.Equals(parentId, id, StringComparison.Ordinal) || current.GetAncestors(parentId).Any(x => String.Equals(x.Id, id, StringComparison.Ordinal)))))
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

    private DiagramReason? CheckResize(string id, DiagramRect bounds, long startVersion)
    {
        if (ReadOnly || !ResizeEnabled)
        {
            return Reason(DiagramReasonKind.Permission);
        }

        if ((Snapshot.Find(id) is not DiagramBox box) || stamps.BoundsChanged(id, startVersion))
        {
            return Reason(DiagramReasonKind.Changed);
        }

        var minimum = Diagram.Index.FindBoxType(box)?.MinSize;
        if (!ScriptTables.CanResize(box, Diagram.Index) ||
            ((minimum is { } min) && ((bounds.Width < min.Width - 0.001) || (bounds.Height < min.Height - 0.001))))
        {
            return Reason(DiagramReasonKind.Permission);
        }

        return SnapshotEditor.IsInside(bounds) ? null : Reason(DiagramReasonKind.Nesting);
    }

    private DiagramReason? CheckConnect(DiagramLinkEnd source, DiagramLinkEnd target, string linkType)
    {
        if (ReadOnly || !ConnectEnabled)
        {
            return Reason(DiagramReasonKind.Permission);
        }

        return DiagramRules.CheckConnection(Snapshot, Diagram.Index, source, target, linkType, static _ => true, null, true);
    }

    private bool CanBeParent(string? parentId) =>
        (parentId is null) || ((Snapshot.Find(parentId) is DiagramGroup { Kind: DiagramGroupKind.Tree, Locked: false } parent) && (Diagram.Index.FindBoxType(parent) is not null));

    private DiagramReason? CheckReconnect(string linkId, DiagramLinkEndSide side, DiagramLinkEnd end, long startVersion, out DiagramLink? current)
    {
        current = null;
        if (ReadOnly || !ConnectEnabled)
        {
            return Reason(DiagramReasonKind.Permission);
        }

        if ((Snapshot.Find(linkId) is not DiagramLink link) || stamps.EndsChanged(linkId, startVersion) || !Snapshot.Contains(end.ElementId))
        {
            return Reason(DiagramReasonKind.Changed);
        }

        if ((ScriptTables.PermissionsOf(link, Diagram.Index) & DiagramPermissions.Connect) == 0)
        {
            return Reason(DiagramReasonKind.Permission);
        }

        current = link;
        var source = EndOf(link, side, end, DiagramLinkEndSide.Source);
        var target = EndOf(link, side, end, DiagramLinkEndSide.Target);
        return DiagramRules.CheckConnection(Snapshot, Diagram.Index, source, target, link.Type, x => !String.Equals(x.Id, linkId, StringComparison.Ordinal), linkId, true);
    }

    private DiagramReason? CheckPlace(DiagramBox? element, DiagramLinkEnd from, string linkType, out DiagramLinkEnd target)
    {
        target = default;
        if (ReadOnly || !ConnectEnabled || (element is null))
        {
            return Reason(DiagramReasonKind.Permission);
        }

        var current = Snapshot;
        var index = Diagram.Index;
        if (!current.Contains(from.ElementId))
        {
            return Reason(DiagramReasonKind.Changed);
        }

        var type = index.FindBoxType(element)!;
        if ((type.MaxCount is { } max) && (current.Groups.Cast<DiagramBox>().Concat(current.Nodes).Count(x => ReferenceEquals(index.FindBoxType(x), type)) >= max))
        {
            return Reason(DiagramReasonKind.Count);
        }

        if (!CanBeParent(element.ParentId))
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
