namespace MusselDiagram.Internal;

using System.Text.Json;

internal static class ScriptTables
{
    public static ScriptTable Build(DiagramSnapshot snapshot, SchemaIndex index, DiagramTemplateSet? templates = null) =>
        new(
            snapshot.Version,
            [.. snapshot.Groups.Select(x => Box(x, index, templates)), .. snapshot.Nodes.Select(x => Box(x, index, templates))],
            [.. snapshot.Links.Select(x => Link(x, index))]);

    public static ScriptChanges Changes(DiagramChangedEventArgs change, SchemaIndex index, DiagramTemplateSet? templates = null)
    {
        var boxes = new List<ScriptBox>();
        var links = new List<ScriptLink>();
        foreach (var id in change.Added.Concat(change.Updated))
        {
            switch (change.After.Find(id))
            {
                case DiagramBox box:
                    boxes.Add(Box(box, index, templates));
                    break;
                case DiagramLink link:
                    links.Add(Link(link, index));
                    break;
            }
        }

        return new ScriptChanges(change.After.Version, boxes, links, change.Deleted);
    }

    public static ScriptSchema Schema(RenderContext context, DiagramTemplateSet? templates = null)
    {
        var schema = context.Index.Schema;
        return new ScriptSchema(
            [.. schema.NodeTypes.Select(x => Type(x, false, context, templates)), .. schema.GroupTypes.Select(x => Type(x, true, context, null))],
            [.. schema.LinkTypes.Select(x => LinkType(x, context))],
            schema.DefaultLinkType,
            Enum.GetValues<DiagramReasonKind>()
                .Select(static x => (Kind: x, Text: DiagramCultures.Reason(x)))
                .Where(static x => x.Text is not null)
                .ToDictionary(static x => JsonNamingPolicy.CamelCase.ConvertName(x.Kind.ToString()), static x => x.Text!, StringComparer.Ordinal),
            new ScriptTexts { SearchPlaceholder = DiagramCultures.SearchPlaceholder, NoMatches = DiagramCultures.NoMatches },
            [.. TypeSearch.InPaletteOrder([.. schema.NodeTypes, .. schema.GroupTypes], context.Index).Select(static x => x.Name)]);
    }

    public static DiagramPermissions PermissionsOf(DiagramBox box, SchemaIndex index) => ElementPermissions.Of(box, index);

    public static DiagramPermissions PermissionsOf(DiagramLink link, SchemaIndex index) => ElementPermissions.Of(link, index);

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

    private static ScriptBox Box(DiagramBox box, SchemaIndex index, DiagramTemplateSet? templates)
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
            Editable = (type is not null) && ((permissions & DiagramPermissions.EditText) != 0),
            Ports = ports.IsEmpty ? null : [.. ports.Select(Port)],
            Texts = (box is DiagramNode) && (type is not null) && (templates?.Find(box.Type) is not null) ? SlotTexts(box, type) : null
        };
    }

    private static Dictionary<string, string> SlotTexts(DiagramBox box, DiagramBoxType type)
    {
        var slots = LabelLayout.SlotsOf(box, type);
        var texts = new Dictionary<string, string>(slots.Length, StringComparer.Ordinal);
        for (var i = 0; i < slots.Length; i++)
        {
            texts[slots[i].Name] = (i == 0 ? box.Label : box.Texts.GetValueOrDefault(slots[i].Name)) ?? string.Empty;
        }

        return texts;
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

    private static ScriptType Type(DiagramBoxType type, bool isGroup, RenderContext context, DiagramTemplateSet? templates)
    {
        var group = type as DiagramGroupType;
        var ports = LinkRouting.Place(type.Ports, type);
        var look = context.Index.LookOf(type);
        return new ScriptType
        {
            Name = type.Name,
            Label = type.DisplayName ?? type.Name,
            Category = type.Category,
            CategoryLabel = type.Category is { } category ? context.Index.Schema.Categories.FirstOrDefault(x => string.Equals(x.Name, category, StringComparison.Ordinal))?.DisplayName ?? category : null,
            Search = type.SearchWords.IsEmpty ? null : [.. type.SearchWords],
            Slots = [.. LabelLayout.SlotsOf(type, isGroup).Select(static x => new ScriptSlot { Name = x.Name, Editable = x.Editable, MaxLines = x.MaxLines ?? 0, Placeholder = x.DefaultText })],
            Group = isGroup,
            Overlay = group?.Kind == DiagramGroupKind.Overlay,
            Content = group?.Sizing == DiagramGroupSizing.Content,
            Padding = group is null ? null : [group.Padding.Left, group.Padding.Top, group.Padding.Right, group.Padding.Bottom],
            W = type.Size.Width,
            H = type.Size.Height,
            MinW = type.MinSize?.Width,
            MinH = type.MinSize?.Height,
            Icon = look.Icon is { } icon ? context.FindIconId(icon) : null,
            HeaderForm = group is null ? null : (group.Header?.Form ?? DiagramHeaderForm.TopLeft) switch
            {
                DiagramHeaderForm.TopCenter => "top-center",
                DiagramHeaderForm.NameOnly => "name-only",
                _ => "top-left"
            },
            HeaderIcon = group is null ? null : group.Header?.IconSize ?? LabelLayout.HeaderIconSize,
            Shape = templates?.Find(type.Name) is not null ? "rectangle" : look.Shape switch
            {
                DiagramShape.Ellipse => "ellipse",
                DiagramShape.Diamond => "diamond",
                DiagramShape.Path => "path",
                _ => "rectangle"
            },
            Path = (templates?.Find(type.Name) is null) && (look.Shape == DiagramShape.Path) ? look.Path : null,
            Style = Svg.ShapeStyle(look),
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

    private static ScriptLinkType LinkType(DiagramLinkType type, RenderContext context)
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
            AllowCycles = type.AllowCycles,
            Style = DiagramLinkView.StyleOf(context.Index.LookOf(type), null),
            SourceMarker = context.FindMarkerId(type.Name, type.SourceMarker),
            TargetMarker = context.FindMarkerId(type.Name, type.TargetMarker)
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
            Along = port.Along,
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
