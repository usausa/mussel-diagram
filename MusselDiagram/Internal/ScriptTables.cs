namespace MusselDiagram.Internal;

using System.Text.Json;

internal static class ScriptTables
{
    private const DiagramPermissions UnregisteredPermissions = DiagramPermissions.Select | DiagramPermissions.Move | DiagramPermissions.Delete;

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

    public static ScriptSchema Schema(RenderContext context)
    {
        var schema = context.Index.Schema;
        return new ScriptSchema(
            [.. schema.NodeTypes.Select(x => Type(x, false, context)), .. schema.GroupTypes.Select(x => Type(x, true, context))],
            [.. schema.LinkTypes.Select(LinkType)],
            schema.DefaultLinkType,
            Enum.GetValues<DiagramReasonKind>()
                .Where(static x => BuiltInTexts.Reason(x) is not null)
                .ToDictionary(static x => JsonNamingPolicy.CamelCase.ConvertName(x.ToString()), static x => BuiltInTexts.Reason(x)!, StringComparer.Ordinal));
    }

    public static DiagramPermissions PermissionsOf(DiagramBox box, SchemaIndex index)
    {
        var type = index.FindBoxType(box);
        var permissions = type is null
            ? (box.Permissions ?? UnregisteredPermissions) & UnregisteredPermissions
            : box.Permissions ?? type.Permissions;
        return box.Locked ? permissions & DiagramPermissions.Select : permissions;
    }

    public static DiagramPermissions PermissionsOf(DiagramLink link, SchemaIndex index)
    {
        var type = index.FindLinkType(link.Type);
        var permissions = type is null
            ? (link.Permissions ?? UnregisteredPermissions) & UnregisteredPermissions
            : link.Permissions ?? type.Permissions;
        return link.Locked ? permissions & DiagramPermissions.Select : permissions;
    }

    public static bool CanMove(DiagramBox box, SchemaIndex index) => (PermissionsOf(box, index) & DiagramPermissions.Move) != 0;

    public static bool CanResize(DiagramBox box, SchemaIndex index) =>
        ((PermissionsOf(box, index) & DiagramPermissions.Resize) != 0) &&
        (box is not DiagramGroup { Kind: DiagramGroupKind.Overlay }) &&
        (index.FindBoxType(box) is not DiagramGroupType { Sizing: DiagramGroupSizing.Content });

    public static string FormName(DiagramLineForm? form) => form switch
    {
        DiagramLineForm.Straight => "straight",
        DiagramLineForm.Curved => "curved",
        _ => "orthogonal"
    };

    private static ScriptBox Box(DiagramBox box, SchemaIndex index)
    {
        var type = index.FindBoxType(box);
        var permissions = PermissionsOf(box, index);
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
            Type = box.Type,
            Kind = kind,
            X = box.Bounds.X,
            Y = box.Bounds.Y,
            W = box.Bounds.Width,
            H = box.Bounds.Height,
            Parent = box.ParentId,
            Members = box is DiagramGroup { MemberIds.IsEmpty: false } group ? [.. group.MemberIds] : null,
            Registered = type is not null,
            Locked = box.Locked,
            Selectable = (permissions & DiagramPermissions.Select) != 0,
            Movable = (permissions & DiagramPermissions.Move) != 0,
            Resizable = CanResize(box, index),
            Connectable = (type is not null) && ((permissions & DiagramPermissions.Connect) != 0),
            Whole = (type is not null) && (type.ConnectsAsWhole || ports.IsEmpty),
            Ports = ports.IsEmpty ? null : [.. ports.Select(Port)]
        };
    }

    private static ScriptLink Link(DiagramLink link, SchemaIndex index)
    {
        var type = index.FindLinkType(link.Type);
        var permissions = PermissionsOf(link, index);
        var (sourceCut, targetCut) = LinkRouting.Cuts(type);
        return new ScriptLink
        {
            Id = link.Id,
            Type = link.Type,
            Source = link.Source.ElementId,
            SourcePort = link.Source.PortId,
            Target = link.Target.ElementId,
            TargetPort = link.Target.PortId,
            Stub = type?.StubLength ?? LinkRouting.DefaultStubLength,
            Form = FormName(type?.Form),
            SourceCut = sourceCut,
            TargetCut = targetCut,
            Selectable = (permissions & DiagramPermissions.Select) != 0,
            Connectable = (type is not null) && ((permissions & DiagramPermissions.Connect) != 0)
        };
    }

    private static ScriptType Type(DiagramBoxType type, bool isGroup, RenderContext context)
    {
        var group = type as DiagramGroupType;
        var ports = LinkRouting.Place(type.Ports);
        return new ScriptType
        {
            Name = type.Name,
            Label = type.DisplayName ?? type.Name,
            Category = type.Category,
            Search = type.SearchWords.IsEmpty ? null : [.. type.SearchWords],
            Group = isGroup,
            Overlay = group?.Kind == DiagramGroupKind.Overlay,
            Content = group?.Sizing == DiagramGroupSizing.Content,
            Padding = group is null ? null : [group.Padding.Left, group.Padding.Top, group.Padding.Right, group.Padding.Bottom],
            W = type.Size.Width,
            H = type.Size.Height,
            MinW = type.MinSize?.Width,
            MinH = type.MinSize?.Height,
            Icon = type.Look.Icon is { } icon ? context.FindIconId(icon) : null,
            Shape = type.Look.Shape switch
            {
                DiagramShape.Ellipse => "ellipse",
                DiagramShape.Diamond => "diamond",
                DiagramShape.Path => "path",
                _ => "rectangle"
            },
            Palette = type.InPalette,
            Parents = type.AllowedParents is { } parents ? [.. parents] : null,
            Children = group?.AllowedChildren is { } children ? [.. children] : null,
            TopLevel = type.TopLevel,
            Stay = type.StayInParent,
            Max = type.MaxCount,
            Whole = type.ConnectsAsWhole || (type.Ports.IsEmpty && (type.GetPorts is null)),
            Connectable = (type.Permissions & DiagramPermissions.Connect) != 0,
            Ports = ports.IsEmpty ? null : [.. ports.Select(Port)]
        };
    }

    private static ScriptLinkType LinkType(DiagramLinkType type)
    {
        var (sourceCut, targetCut) = LinkRouting.Cuts(type);
        return new ScriptLinkType
        {
            Name = type.Name,
            Form = FormName(type.Form),
            Stub = type.StubLength,
            SourceCut = sourceCut,
            TargetCut = targetCut,
            AllowSelf = type.AllowSelf,
            AllowDuplicate = type.AllowDuplicate,
            AllowCrossParent = type.AllowCrossParent,
            AllowCycles = type.AllowCycles
        };
    }

    private static ScriptPort Port(PortPlacement port)
    {
        var definition = port.Definition;
        return new ScriptPort
        {
            Id = port.Id,
            Side = Side(port.Side),
            Ratio = port.Ratio,
            Offset = port.Offset,
            Direction = definition.Direction switch
            {
                DiagramPortDirection.In => "in",
                DiagramPortDirection.Out => "out",
                _ => "both"
            },
            Kind = definition.Kind,
            Max = definition.MaxLinks,
            Accepts = definition.Accepts is { } accepts ? [.. accepts] : null,
            Rejects = definition.Rejects is { } rejects ? [.. rejects] : null,
            LinkType = definition.LinkType,
            Label = definition.Label,
            Hidden = definition.Visibility == DiagramPortVisibility.WhileConnecting
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
