namespace MusselDiagram.Internal;

internal static class ScriptTables
{
    public static ScriptTable Build(DiagramSnapshot snapshot, SchemaIndex index) =>
        new(
            snapshot.Version,
            [.. snapshot.Groups.Select(x => Box(x, index)), .. snapshot.Nodes.Select(x => Box(x, index))],
            [.. snapshot.Links.Select(x => Link(x, index))]);

    public static ScriptChanges Changes(DiagramChangedEventArgs change, SchemaIndex index)
    {
        var boxes = new List<ScriptBox>();
        var links = new List<ScriptLink>();
        foreach (var id in change.Added.Concat(change.Updated))
        {
            switch (change.After.Find(id))
            {
                case DiagramBox box:
                    boxes.Add(Box(box, index));
                    break;
                case DiagramLink link:
                    links.Add(Link(link, index));
                    break;
            }
        }

        return new ScriptChanges(change.After.Version, boxes, links, change.Deleted);
    }

    public static bool CanMove(DiagramBox box, SchemaIndex index) =>
        !box.Locked && Permissions(box.Permissions, index.FindBoxType(box)).HasFlag(DiagramPermissions.Move);

    private static DiagramPermissions Permissions(DiagramPermissions? own, DiagramType? type) => own ?? type?.Permissions ?? DiagramPermissions.All;

    private static ScriptBox Box(DiagramBox box, SchemaIndex index)
    {
        var type = index.FindBoxType(box);
        var ports = index.PortsOf(box);
        var kind = box switch
        {
            DiagramGroup { Kind: DiagramGroupKind.Overlay } => "overlay",
            DiagramGroup => "group",
            _ => "node"
        };
        return new ScriptBox
        {
            Id = box.Id,
            Kind = kind,
            X = box.Bounds.X,
            Y = box.Bounds.Y,
            W = box.Bounds.Width,
            H = box.Bounds.Height,
            Parent = box.ParentId,
            Members = box is DiagramGroup { MemberIds.IsEmpty: false } group ? [.. group.MemberIds] : null,
            Selectable = Permissions(box.Permissions, type).HasFlag(DiagramPermissions.Select),
            Movable = CanMove(box, index),
            Ports = ports.IsEmpty ? null : [.. ports.Select(static x => new ScriptPort { Id = x.Id, Side = Side(x.Side), Ratio = x.Ratio, Offset = x.Offset })]
        };
    }

    private static ScriptLink Link(DiagramLink link, SchemaIndex index)
    {
        var type = index.FindLinkType(link.Type);
        return new ScriptLink
        {
            Id = link.Id,
            Source = link.Source.ElementId,
            SourcePort = link.Source.PortId,
            Target = link.Target.ElementId,
            TargetPort = link.Target.PortId,
            Stub = type?.StubLength ?? LinkRouting.DefaultStubLength,
            Straight = type?.Form == DiagramLineForm.Straight,
            Selectable = Permissions(link.Permissions, type).HasFlag(DiagramPermissions.Select)
        };
    }

    private static string Side(DiagramSide side) => side switch
    {
        DiagramSide.Top => "top",
        DiagramSide.Right => "right",
        DiagramSide.Bottom => "bottom",
        _ => "left"
    };
}
