namespace MusselDiagram.Internal;

internal readonly record struct ConnectionPoint(DiagramBox Element, DiagramBoxType Type, PortPlacement? Port);

internal static class DiagramRules
{
    private static readonly DiagramLinkType DefaultLinkType = new() { Name = string.Empty };

    public static ConnectionPoint? Resolve(DiagramSnapshot snapshot, SchemaIndex index, DiagramLinkEnd end)
    {
        if ((snapshot.Find(end.ElementId) is not DiagramBox box) || (index.FindBoxType(box) is not { } type))
        {
            return null;
        }

        if (end.PortId is null)
        {
            return type.ConnectsAsWhole || index.PortsOf(box).IsEmpty ? new ConnectionPoint(box, type, null) : null;
        }

        return index.FindPort(box, end.PortId) is { } port ? new ConnectionPoint(box, type, port) : null;
    }

    public static DiagramReason? CheckConnection(
        DiagramSnapshot snapshot,
        SchemaIndex index,
        DiagramLinkEnd sourceEnd,
        DiagramLinkEnd targetEnd,
        string linkTypeName,
        Func<DiagramLink, bool> counts,
        string? exceptLinkId,
        bool checkPermissions)
    {
        if ((Resolve(snapshot, index, sourceEnd) is not { } source) || (Resolve(snapshot, index, targetEnd) is not { } target))
        {
            return Reason(DiagramReasonKind.NoConnectionPoint);
        }

        if (!Sends(source.Port) || !Receives(target.Port))
        {
            return Reason(DiagramReasonKind.Direction);
        }

        if ((source.Port?.Definition.Kind is { } sourceKind) && (target.Port?.Definition.Kind is { } targetKind) && !string.Equals(sourceKind, targetKind, StringComparison.Ordinal))
        {
            return Reason(DiagramReasonKind.PortKind);
        }

        if (checkPermissions && (!CanConnect(source) || !CanConnect(target)))
        {
            return Reason(DiagramReasonKind.Permission);
        }

        if (AtLimit(snapshot, sourceEnd, source.Port, counts) || AtLimit(snapshot, targetEnd, target.Port, counts))
        {
            return Reason(DiagramReasonKind.Limit);
        }

        var linkType = index.FindLinkType(linkTypeName) ?? DefaultLinkType;
        if (!linkType.AllowSelf && string.Equals(sourceEnd.ElementId, targetEnd.ElementId, StringComparison.Ordinal))
        {
            return Reason(DiagramReasonKind.SelfOrDuplicate);
        }

        if (!linkType.AllowDuplicate && snapshot.GetLinks(sourceEnd.ElementId, DiagramLinkDirection.Out).Any(x => (x.Source == sourceEnd) && (x.Target == targetEnd) && counts(x)))
        {
            return Reason(DiagramReasonKind.SelfOrDuplicate);
        }

        if (!Accepts(source.Port, target.Element.Type) || !Accepts(target.Port, source.Element.Type))
        {
            return Reason(DiagramReasonKind.PeerType);
        }

        if (!linkType.AllowCrossParent && !string.Equals(source.Element.ParentId, target.Element.ParentId, StringComparison.Ordinal))
        {
            return Reason(DiagramReasonKind.CrossParent);
        }

        if (!linkType.AllowCycles && MakesCycle(snapshot, sourceEnd.ElementId, targetEnd.ElementId, linkTypeName, exceptLinkId))
        {
            return Reason(DiagramReasonKind.Cycle);
        }

        if (index.Schema.Rules.CanConnect is { } canConnect)
        {
            var result = canConnect(new DiagramConnectionContext { Snapshot = snapshot, Source = sourceEnd, Target = targetEnd, LinkType = linkTypeName, Reconnecting = exceptLinkId });
            if (!result.Allowed)
            {
                return new DiagramReason(DiagramReasonKind.App, result.Reason);
            }
        }

        return null;
    }

    public static DiagramReason? CheckNesting(DiagramSnapshot snapshot, SchemaIndex index, string? elementId, string typeName, string? parentId)
    {
        var type = (elementId is not null) && (snapshot.Find(elementId) is DiagramBox box) ? index.FindBoxType(box) : index.FindBoxType(typeName);
        if (parentId is null)
        {
            if (type is { TopLevel: false })
            {
                return Reason(DiagramReasonKind.Nesting);
            }
        }
        else
        {
            if (snapshot.Find(parentId) is not DiagramGroup { Kind: DiagramGroupKind.Tree } parent)
            {
                return Reason(DiagramReasonKind.Nesting);
            }

            if ((index.FindBoxType(parent) is DiagramGroupType { AllowedChildren: { } children }) && !children.Contains(typeName))
            {
                return Reason(DiagramReasonKind.Nesting);
            }

            if ((type?.AllowedParents is { } parents) && !parents.Contains(parent.Type))
            {
                return Reason(DiagramReasonKind.Nesting);
            }
        }

        if (index.Schema.Rules.CanNest is { } canNest)
        {
            var result = canNest(new DiagramNestingContext { Snapshot = snapshot, ElementId = elementId, Type = typeName, ParentId = parentId });
            if (!result.Allowed)
            {
                return new DiagramReason(DiagramReasonKind.App, result.Reason);
            }
        }

        return null;
    }

    public static IEnumerable<string> References(DiagramElement element, SchemaIndex index)
    {
        if ((element.Data is null) || (index.FindType(element) is not { GetReferences: { } getReferences } type))
        {
            return [];
        }

        if ((type.DataType is { } dataType) && !dataType.Type.IsInstanceOfType(element.Data))
        {
            return [];
        }

        return getReferences(element.Data);
    }

    private static DiagramReason Reason(DiagramReasonKind kind) => new(kind, DiagramCultures.Reason(kind));

    private static bool Sends(PortPlacement? port) => (port is null) || (port.Definition.Direction != DiagramPortDirection.In);

    private static bool Receives(PortPlacement? port) => (port is null) || (port.Definition.Direction != DiagramPortDirection.Out);

    private static bool CanConnect(ConnectionPoint point) =>
        !point.Element.Locked && (((point.Element.Permissions ?? point.Type.Permissions) & DiagramPermissions.Connect) != 0);

    private static bool AtLimit(DiagramSnapshot snapshot, DiagramLinkEnd end, PortPlacement? port, Func<DiagramLink, bool> counts) =>
        (port?.Definition.MaxLinks is { } max) && (snapshot.GetLinks(end.ElementId, DiagramLinkDirection.Both, end.PortId).Count(counts) >= max);

    private static bool Accepts(PortPlacement? port, string peerType)
    {
        if (port is null)
        {
            return true;
        }

        var definition = port.Definition;
        return ((definition.Accepts is not { } accepts) || accepts.Contains(peerType)) && ((definition.Rejects is not { } rejects) || !rejects.Contains(peerType));
    }

    private static bool MakesCycle(DiagramSnapshot snapshot, string sourceId, string targetId, string linkType, string? exceptLinkId)
    {
        if (string.Equals(sourceId, targetId, StringComparison.Ordinal))
        {
            return true;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal) { targetId };
        var queue = new Queue<string>();
        queue.Enqueue(targetId);
        while (queue.Count > 0)
        {
            foreach (var link in snapshot.GetLinks(queue.Dequeue(), DiagramLinkDirection.Out))
            {
                if (!string.Equals(link.Type, linkType, StringComparison.Ordinal) || string.Equals(link.Id, exceptLinkId, StringComparison.Ordinal))
                {
                    continue;
                }

                var next = link.Target.ElementId;
                if (string.Equals(next, sourceId, StringComparison.Ordinal))
                {
                    return true;
                }

                if (seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return false;
    }
}
