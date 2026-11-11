namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal sealed class SnapshotEditor
{
    private readonly SchemaIndex index;

    private readonly ImmutableDictionary<string, DiagramElement>.Builder elements;

    private readonly List<DiagramNode> nodes;

    private readonly List<DiagramGroup> groups;

    private readonly List<DiagramLink> links;

    private readonly List<string> added = [];

    private readonly List<string> deleted = [];

    private readonly Dictionary<string, DiagramChangeParts> updated = new(StringComparer.Ordinal);

    public SnapshotEditor(DiagramSnapshot source, SchemaIndex index)
    {
        this.index = index;
        elements = source.Elements.ToBuilder();
        nodes = [.. source.Nodes];
        groups = [.. source.Groups];
        links = [.. source.Links];
    }

    public bool HasChanges => (added.Count > 0) || (deleted.Count > 0) || (updated.Count > 0);

    public DiagramSnapshot Build(long version) => new(version, elements.ToImmutable(), [.. nodes], [.. groups], [.. links]);

    public DiagramChangedEventArgs CreateArgs(DiagramSnapshot before, DiagramSnapshot after, DiagramChangeOrigin origin, object? tag) =>
        new(before, after, origin, tag, [.. added], [.. deleted], updated.ToImmutableDictionary(StringComparer.Ordinal));

    public void Add(DiagramElement element)
    {
        if (string.IsNullOrEmpty(element.Id))
        {
            throw new DiagramException("An element has an empty ID.");
        }

        if (elements.ContainsKey(element.Id))
        {
            throw new DiagramException($"The ID '{element.Id}' is already used.");
        }

        switch (element)
        {
            case DiagramGroup group:
                ValidateBox(group);
                elements[group.Id] = group;
                groups.Add(group);
                break;
            case DiagramNode node:
                ValidateBox(node);
                elements[node.Id] = node;
                nodes.Add(node);
                break;
            case DiagramLink link:
                ValidateLink(link);
                var routed = RouteOf(link);
                elements[routed.Id] = routed;
                links.Add(routed);
                break;
            default:
                throw new DiagramException($"The element '{element.Id}' is not supported.");
        }

        added.Add(element.Id);
    }

    public void Delete(IEnumerable<string> ids)
    {
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var children = ChildrenMap();
        foreach (var id in ids)
        {
            Collect(id);
        }

        if (targets.Count == 0)
        {
            return;
        }

        foreach (var link in links)
        {
            if (targets.Contains(link.Source.ElementId) || targets.Contains(link.Target.ElementId))
            {
                targets.Add(link.Id);
            }
        }

        foreach (var group in groups.ToArray())
        {
            if ((group.Kind != DiagramGroupKind.Overlay) || targets.Contains(group.Id) || !group.MemberIds.Any(targets.Contains))
            {
                continue;
            }

            var remaining = group.MemberIds.RemoveAll(targets.Contains);
            if (remaining.IsEmpty)
            {
                targets.Add(group.Id);
            }
            else
            {
                Put(group with { MemberIds = remaining });
                Mark(group.Id, DiagramChangeParts.Members);
            }
        }

        nodes.RemoveAll(x => targets.Contains(x.Id));
        groups.RemoveAll(x => targets.Contains(x.Id));
        links.RemoveAll(x => targets.Contains(x.Id));
        foreach (var id in targets)
        {
            elements.Remove(id);
            updated.Remove(id);
            if (!added.Remove(id))
            {
                deleted.Add(id);
            }
        }

        void Collect(string id)
        {
            if (!elements.ContainsKey(id) || !targets.Add(id) || !children.TryGetValue(id, out var list))
            {
                return;
            }

            foreach (var child in list)
            {
                Collect(child);
            }
        }
    }

    public void Replace(DiagramElement element)
    {
        var current = elements.GetValueOrDefault(element.Id) ?? throw new DiagramException($"The element '{element.Id}' does not exist.");
        if (current.GetType() != element.GetType())
        {
            throw new DiagramException($"The element '{element.Id}' cannot change into another kind of element.");
        }

        if (current == element)
        {
            return;
        }

        switch (element)
        {
            case DiagramBox box:
                ValidateBox(box);
                var boxParts = BoxParts((DiagramBox)current, box);
                Put(box);
                Mark(box.Id, boxParts);
                if ((boxParts & (DiagramChangeParts.Position | DiagramChangeParts.Size)) != 0)
                {
                    Reroute([box.Id]);
                }

                break;
            case DiagramLink link:
                ValidateLink(link);
                var linkParts = LinkParts((DiagramLink)current, link);
                Put((linkParts & DiagramChangeParts.Ends) != 0 ? RouteOf(link) : link);
                Mark(link.Id, (linkParts & DiagramChangeParts.Ends) != 0 ? linkParts | DiagramChangeParts.Route : linkParts);
                break;
        }
    }

    public void Move(IEnumerable<string> ids, DiagramPoint offset)
    {
        if (offset == default)
        {
            return;
        }

        var moving = new HashSet<string>(StringComparer.Ordinal);
        var children = ChildrenMap();
        foreach (var id in ids)
        {
            switch (elements.GetValueOrDefault(id))
            {
                case DiagramGroup { Kind: DiagramGroupKind.Overlay } overlay:
                    foreach (var member in overlay.MemberIds)
                    {
                        Collect(member);
                    }

                    break;
                case DiagramBox box:
                    Collect(box.Id);
                    break;
            }
        }

        if (moving.Count == 0)
        {
            return;
        }

        foreach (var id in moving)
        {
            var box = (DiagramBox)elements[id];
            Put(box with { Bounds = box.Bounds.Offset(offset) });
            Mark(id, DiagramChangeParts.Position);
        }

        Reroute(moving);

        void Collect(string id)
        {
            if (elements.GetValueOrDefault(id) is not DiagramBox || !moving.Add(id) || !children.TryGetValue(id, out var list))
            {
                return;
            }

            foreach (var child in list)
            {
                Collect(child);
            }
        }
    }

    private static DiagramChangeParts BoxParts(DiagramBox before, DiagramBox after)
    {
        var parts = DiagramChangeParts.None;
        if (new DiagramPoint(before.Bounds.X, before.Bounds.Y) != new DiagramPoint(after.Bounds.X, after.Bounds.Y))
        {
            parts |= DiagramChangeParts.Position;
        }

        if (new DiagramSize(before.Bounds.Width, before.Bounds.Height) != new DiagramSize(after.Bounds.Width, after.Bounds.Height))
        {
            parts |= DiagramChangeParts.Size;
        }

        if (!string.Equals(before.ParentId, after.ParentId, StringComparison.Ordinal))
        {
            parts |= DiagramChangeParts.Parent;
        }

        if (!string.Equals(before.Label, after.Label, StringComparison.Ordinal) || !ReferenceEquals(before.Texts, after.Texts))
        {
            parts |= DiagramChangeParts.Label;
        }

        if (!Equals(before.Data, after.Data))
        {
            parts |= DiagramChangeParts.Data;
        }

        if ((before is DiagramGroup beforeGroup) && (after is DiagramGroup afterGroup) && !beforeGroup.MemberIds.SequenceEqual(afterGroup.MemberIds))
        {
            parts |= DiagramChangeParts.Members;
        }

        return parts;
    }

    private static DiagramChangeParts LinkParts(DiagramLink before, DiagramLink after)
    {
        var parts = DiagramChangeParts.None;
        if ((before.Source != after.Source) || (before.Target != after.Target))
        {
            parts |= DiagramChangeParts.Ends;
        }

        if (!before.Route.SequenceEqual(after.Route))
        {
            parts |= DiagramChangeParts.Route;
        }

        if (!Equals(before.Data, after.Data))
        {
            parts |= DiagramChangeParts.Data;
        }

        return parts;
    }

    private void ValidateBox(DiagramBox box)
    {
        var overlay = box is DiagramGroup { Kind: DiagramGroupKind.Overlay };
        var bounds = box.Bounds;
        if (!overlay && (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) || (bounds.Width <= 0) || (bounds.Height <= 0)))
        {
            throw new DiagramException($"The bounds of '{box.Id}' are not finite numbers with a positive size.");
        }

        if (box is DiagramGroup group)
        {
            ValidateGroup(group);
        }

        if (box.ParentId is not { } parentId)
        {
            return;
        }

        if (elements.GetValueOrDefault(parentId) is not DiagramGroup { Kind: DiagramGroupKind.Tree })
        {
            throw new DiagramException($"The parent '{parentId}' of '{box.Id}' is not a tree group.");
        }

        var current = (string?)parentId;
        for (var steps = 0; (current is not null) && (steps <= elements.Count); steps++)
        {
            if (string.Equals(current, box.Id, StringComparison.Ordinal))
            {
                throw new DiagramException($"The parents of '{box.Id}' form a cycle.");
            }

            current = (elements.GetValueOrDefault(current) as DiagramBox)?.ParentId;
        }
    }

    private void ValidateGroup(DiagramGroup group)
    {
        if ((index.FindGroupType(group.Type) is { } type) && (type.Kind != group.Kind))
        {
            throw new DiagramException($"The kind of the group '{group.Id}' differs from the kind of its type.");
        }

        if (group.Kind == DiagramGroupKind.Tree)
        {
            if (!group.MemberIds.IsEmpty)
            {
                throw new DiagramException($"The tree group '{group.Id}' has members.");
            }

            return;
        }

        if (group.ParentId is not null)
        {
            throw new DiagramException($"The overlay group '{group.Id}' has a parent.");
        }

        foreach (var memberId in group.MemberIds)
        {
            if (elements.GetValueOrDefault(memberId) is not (DiagramNode or DiagramGroup { Kind: DiagramGroupKind.Tree }))
            {
                throw new DiagramException($"The member '{memberId}' of '{group.Id}' is not a node or a tree group.");
            }
        }
    }

    private void ValidateLink(DiagramLink link)
    {
        ValidateEnd(link, link.Source);
        ValidateEnd(link, link.Target);
    }

    private void ValidateEnd(DiagramLink link, DiagramLinkEnd end)
    {
        if (elements.GetValueOrDefault(end.ElementId) is not DiagramBox box)
        {
            throw new DiagramException($"The end '{end.ElementId}' of '{link.Id}' is not a node or a group.");
        }

        if ((end.PortId is { } portId) && (index.FindBoxType(box) is { } type) && !type.Ports.Any(x => string.Equals(x.Id, portId, StringComparison.Ordinal)))
        {
            throw new DiagramException($"The port '{portId}' of '{box.Id}' does not exist.");
        }
    }

    private DiagramLink RouteOf(DiagramLink link)
    {
        var source = (DiagramBox)elements[link.Source.ElementId];
        var target = (DiagramBox)elements[link.Target.ElementId];
        var route = LinkRouting.Route(source, index.FindPort(source, link.Source.PortId), target, index.FindPort(target, link.Target.PortId), index.FindLinkType(link.Type));
        return link with { Route = route, RouteState = DiagramRouteState.Routed, RouteError = null };
    }

    private void Reroute(IReadOnlyCollection<string> ids)
    {
        foreach (var link in links.ToArray())
        {
            if (!ids.Contains(link.Source.ElementId) && !ids.Contains(link.Target.ElementId))
            {
                continue;
            }

            var routed = RouteOf(link);
            if (routed.Route.SequenceEqual(link.Route))
            {
                continue;
            }

            Put(routed);
            Mark(link.Id, DiagramChangeParts.Route);
        }
    }

    private Dictionary<string, List<string>> ChildrenMap()
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var box in groups.Cast<DiagramBox>().Concat(nodes))
        {
            if (box.ParentId is null)
            {
                continue;
            }

            if (!map.TryGetValue(box.ParentId, out var list))
            {
                list = [];
                map[box.ParentId] = list;
            }

            list.Add(box.Id);
        }

        return map;
    }

    private void Put(DiagramElement element)
    {
        elements[element.Id] = element;
        switch (element)
        {
            case DiagramNode node:
                nodes[nodes.FindIndex(x => string.Equals(x.Id, node.Id, StringComparison.Ordinal))] = node;
                break;
            case DiagramGroup group:
                groups[groups.FindIndex(x => string.Equals(x.Id, group.Id, StringComparison.Ordinal))] = group;
                break;
            case DiagramLink link:
                links[links.FindIndex(x => string.Equals(x.Id, link.Id, StringComparison.Ordinal))] = link;
                break;
        }
    }

    private void Mark(string id, DiagramChangeParts parts)
    {
        if (added.Contains(id))
        {
            return;
        }

        updated[id] = updated.GetValueOrDefault(id) | parts;
    }
}
