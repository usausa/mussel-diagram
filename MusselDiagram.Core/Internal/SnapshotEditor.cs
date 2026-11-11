namespace MusselDiagram.Internal;

using System.Collections.Immutable;
using System.Text.Json;

internal sealed class SnapshotEditor
{
    private const double Tolerance = 1e-6;

    private static readonly DiagramThickness DefaultPadding = new DiagramGroupType { Name = string.Empty }.Padding;

    private readonly SchemaIndex index;

    private readonly ImmutableDictionary<string, DiagramElement>.Builder elements;

    private readonly List<DiagramNode> nodes;

    private readonly List<DiagramGroup> groups;

    private readonly List<DiagramLink> links;

    private readonly List<DiagramSharedItem> sharedItems;

    private readonly HashSet<string> touched = new(StringComparer.Ordinal);

    private Dictionary<string, int>? nodePositions;

    private Dictionary<string, int>? groupPositions;

    private Dictionary<string, int>? linkPositions;

    private Dictionary<string, int>? sharedItemPositions;

    private bool reorder;

    public SnapshotEditor(DiagramSnapshot source, SchemaIndex index)
    {
        this.index = index;
        elements = source.Elements.ToBuilder();
        nodes = [.. source.Nodes];
        groups = [.. source.Groups];
        links = [.. source.Links];
        sharedItems = [.. source.SharedItems];
    }

    public bool SkipKindCheck { get; init; }

    public bool SkipCycleCheck { get; init; }

    public ChangeSet Changes { get; } = new();

    public DiagramElement? Find(string id) => elements.GetValueOrDefault(id);

    public DiagramSnapshot Build(long version) =>
        new(version, elements.ToImmutable(), [.. nodes], [.. groups], [.. links], [.. sharedItems]);

    public void Complete()
    {
        if (reorder)
        {
            ReorderGroups();
        }

        ResizeGroups();
        touched.Clear();
        reorder = false;
    }

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

        ValidateData(element);
        switch (element)
        {
            case DiagramGroup group:
                ValidateBox(group);
                elements[group.Id] = group;
                Append(groups, groupPositions, group);
                reorder |= group.ParentId is not null;
                touched.Add(group.Id);
                break;
            case DiagramNode node:
                ValidateBox(node);
                elements[node.Id] = node;
                Append(nodes, nodePositions, node);
                touched.Add(node.Id);
                break;
            case DiagramLink link:
                ValidateLink(link);
                var routed = RouteOf(link);
                elements[routed.Id] = routed;
                Append(links, linkPositions, routed);
                break;
            case DiagramSharedItem item:
                elements[item.Id] = item;
                Append(sharedItems, sharedItemPositions, item);
                break;
            default:
                throw new DiagramException($"The element '{element.Id}' is not supported.");
        }

        Changes.MarkAdded(element.Id);
    }

    public void AddWithRoute(DiagramLink link)
    {
        Add(link);
        if (link.Route.Length < 2)
        {
            return;
        }

        var routed = (DiagramLink)elements[link.Id];
        if (Near(routed.Route[0], link.Route[0]) && Near(routed.Route[^1], link.Route[^1]))
        {
            Put(routed with { Route = link.Route });
        }
    }

    public void Delete(IEnumerable<string> ids)
    {
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var children = ChildrenMap();
        foreach (var id in ids)
        {
            Collect(id);
        }

        if (targets.Count > 0)
        {
            Remove(targets);
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

        if ((current is DiagramGroup currentGroup) && (element is DiagramGroup newGroup) && (currentGroup.Kind != newGroup.Kind))
        {
            throw new DiagramException($"The kind of the group '{element.Id}' cannot change.");
        }

        ValidateData(element);
        switch (element)
        {
            case DiagramBox box:
                ValidateBox(box);
                var before = (DiagramBox)current;
                var boxParts = BoxParts(before, box);
                Put(box);
                Changes.MarkUpdated(box.Id, boxParts);
                if ((boxParts & (DiagramChangeParts.Position | DiagramChangeParts.Size | DiagramChangeParts.Parent | DiagramChangeParts.Members)) != 0)
                {
                    touched.Add(box.Id);
                    if (before.ParentId is { } formerParent)
                    {
                        touched.Add(formerParent);
                    }
                }

                reorder |= (box is DiagramGroup) && ((boxParts & DiagramChangeParts.Parent) != 0);
                RemoveLinksToLostPorts(before, box);
                Reroute([box.Id]);
                break;
            case DiagramLink link:
                ValidateLink(link);
                var old = (DiagramLink)current;
                var linkParts = LinkParts(old, link);
                var reroute = ((linkParts & DiagramChangeParts.Ends) != 0) || !string.Equals(old.Type, link.Type, StringComparison.Ordinal);
                Put(reroute ? RouteOf(link) : link);
                Changes.MarkUpdated(link.Id, reroute ? linkParts | DiagramChangeParts.Route : linkParts);
                break;
            case DiagramSharedItem item:
                Put(item);
                Changes.MarkUpdated(item.Id, SharedItemParts((DiagramSharedItem)current, item));
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
            Changes.MarkUpdated(id, DiagramChangeParts.Position);
            touched.Add(id);
        }

        Reroute(moving);

        void Collect(string id)
        {
            if ((elements.GetValueOrDefault(id) is not DiagramBox) || !moving.Add(id) || !children.TryGetValue(id, out var list))
            {
                return;
            }

            foreach (var child in list)
            {
                Collect(child);
            }
        }
    }

    public void SetParent(IEnumerable<string> ids, string? parentId)
    {
        foreach (var id in ids)
        {
            if (elements.GetValueOrDefault(id) is not DiagramBox box)
            {
                throw new DiagramException($"The element '{id}' is not a node or a group.");
            }

            if (string.Equals(box.ParentId, parentId, StringComparison.Ordinal))
            {
                continue;
            }

            var moved = box with { ParentId = parentId };
            ValidateBox(moved);
            if (box.ParentId is { } formerParent)
            {
                touched.Add(formerParent);
            }

            Put(moved);
            Changes.MarkUpdated(id, DiagramChangeParts.Parent);
            touched.Add(id);
            reorder |= box is DiagramGroup;
        }
    }

    public void SetMembers(string groupId, IEnumerable<string> memberIds)
    {
        if (elements.GetValueOrDefault(groupId) is not DiagramGroup { Kind: DiagramGroupKind.Overlay } group)
        {
            throw new DiagramException($"The element '{groupId}' is not an overlay group.");
        }

        ImmutableArray<string> members = [.. memberIds.Distinct(StringComparer.Ordinal)];
        if (members.SequenceEqual(group.MemberIds, StringComparer.Ordinal))
        {
            return;
        }

        if (members.IsEmpty)
        {
            Remove(new HashSet<string>(StringComparer.Ordinal) { groupId });
            return;
        }

        var changed = group with { MemberIds = members };
        ValidateBox(changed);
        Put(changed);
        Changes.MarkUpdated(groupId, DiagramChangeParts.Members);
        touched.Add(groupId);
    }

    internal static DiagramChangeParts PartsOf(DiagramElement before, DiagramElement after) => (before, after) switch
    {
        (DiagramBox x, DiagramBox y) => BoxParts(x, y),
        (DiagramLink x, DiagramLink y) => LinkParts(x, y),
        (DiagramSharedItem x, DiagramSharedItem y) => SharedItemParts(x, y),
        _ => DiagramChangeParts.None
    };

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

        if (!string.Equals(before.Label, after.Label, StringComparison.Ordinal) || !SnapshotContent.SameTexts(before.Texts, after.Texts))
        {
            parts |= DiagramChangeParts.Label;
        }

        if (!SnapshotContent.SameData(before.Data, after.Data))
        {
            parts |= DiagramChangeParts.Data;
        }

        if ((before is DiagramGroup beforeGroup) && (after is DiagramGroup afterGroup) && !beforeGroup.MemberIds.SequenceEqual(afterGroup.MemberIds, StringComparer.Ordinal))
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

        if (!SnapshotContent.SameData(before.Data, after.Data))
        {
            parts |= DiagramChangeParts.Data;
        }

        return parts;
    }

    private static DiagramChangeParts SharedItemParts(DiagramSharedItem before, DiagramSharedItem after)
    {
        var parts = DiagramChangeParts.None;
        if (!string.Equals(before.Label, after.Label, StringComparison.Ordinal))
        {
            parts |= DiagramChangeParts.Label;
        }

        if (!SnapshotContent.SameData(before.Data, after.Data))
        {
            parts |= DiagramChangeParts.Data;
        }

        return parts;
    }

    private static DiagramRect Union(IEnumerable<DiagramRect> rects)
    {
        var left = double.PositiveInfinity;
        var top = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        var bottom = double.NegativeInfinity;
        foreach (var rect in rects)
        {
            left = Math.Min(left, rect.X);
            top = Math.Min(top, rect.Y);
            right = Math.Max(right, rect.Right);
            bottom = Math.Max(bottom, rect.Bottom);
        }

        return new DiagramRect(left, top, right - left, bottom - top);
    }

    private static DiagramRect Expand(DiagramRect rect, DiagramThickness padding) =>
        new(rect.X - padding.Left, rect.Y - padding.Top, rect.Width + padding.Left + padding.Right, rect.Height + padding.Top + padding.Bottom);

    private static bool Contains(DiagramRect outer, DiagramRect inner) =>
        (outer.X <= inner.X + Tolerance) && (outer.Y <= inner.Y + Tolerance) && (outer.Right >= inner.Right - Tolerance) && (outer.Bottom >= inner.Bottom - Tolerance);

    private static bool Near(double x, double y) => Math.Abs(x - y) <= Tolerance;

    private static void Append<T>(List<T> list, Dictionary<string, int>? positions, T element)
        where T : DiagramElement
    {
        positions?.Add(element.Id, list.Count);
        list.Add(element);
    }

    private static void Set<T>(List<T> list, ref Dictionary<string, int>? positions, T element)
        where T : DiagramElement
    {
        if (positions is null)
        {
            positions = new Dictionary<string, int>(list.Count, StringComparer.Ordinal);
            for (var i = 0; i < list.Count; i++)
            {
                positions[list[i].Id] = i;
            }
        }

        list[positions[element.Id]] = element;
    }

    private static bool Near(DiagramPoint x, DiagramPoint y) => Near(x.X, y.X) && Near(x.Y, y.Y);

    private static bool Near(DiagramRect x, DiagramRect y) =>
        Near(x.X, y.X) && Near(x.Y, y.Y) && Near(x.Width, y.Width) && Near(x.Height, y.Height);

    private void Remove(HashSet<string> targets)
    {
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
                Changes.MarkUpdated(group.Id, DiagramChangeParts.Members);
                touched.Add(group.Id);
            }
        }

        foreach (var link in links)
        {
            if (targets.Contains(link.Source.ElementId) || targets.Contains(link.Target.ElementId))
            {
                targets.Add(link.Id);
            }
        }

        foreach (var id in targets)
        {
            if ((elements.GetValueOrDefault(id) is DiagramBox { ParentId: { } parentId }) && !targets.Contains(parentId))
            {
                touched.Add(parentId);
            }
        }

        nodes.RemoveAll(x => targets.Contains(x.Id));
        groups.RemoveAll(x => targets.Contains(x.Id));
        links.RemoveAll(x => targets.Contains(x.Id));
        sharedItems.RemoveAll(x => targets.Contains(x.Id));
        nodePositions = null;
        groupPositions = null;
        linkPositions = null;
        sharedItemPositions = null;
        foreach (var id in targets)
        {
            elements.Remove(id);
            touched.Remove(id);
            Changes.MarkDeleted(id);
        }
    }

    private void RemoveLinksToLostPorts(DiagramBox before, DiagramBox after)
    {
        if (index.FindBoxType(after) is null)
        {
            return;
        }

        var beforePorts = index.PortsOf(before);
        var afterPorts = index.PortsOf(after);
        if (beforePorts == afterPorts)
        {
            return;
        }

        var kept = afterPorts.Select(static x => x.Id).ToHashSet(StringComparer.Ordinal);
        var lost = beforePorts.Select(static x => x.Id).Where(x => !kept.Contains(x)).ToHashSet(StringComparer.Ordinal);
        if (lost.Count == 0)
        {
            return;
        }

        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in links)
        {
            if (LosesEnd(link.Source) || LosesEnd(link.Target))
            {
                targets.Add(link.Id);
            }
        }

        if (targets.Count > 0)
        {
            Remove(targets);
        }

        bool LosesEnd(DiagramLinkEnd end) =>
            string.Equals(end.ElementId, after.Id, StringComparison.Ordinal) && (end.PortId is not null) && lost.Contains(end.PortId);
    }

    private void ValidateData(DiagramElement element)
    {
        if (element.Data is null or JsonElement)
        {
            return;
        }

        if ((index.FindType(element)?.DataType is { } dataType) && dataType.Type.IsInstanceOfType(element.Data))
        {
            return;
        }

        throw new DiagramException($"The data of '{element.Id}' is neither the registered data type nor a JsonElement.");
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

        if (SkipCycleCheck)
        {
            return;
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
        if (!SkipKindCheck && (index.FindGroupType(group.Type) is { } type) && (type.Kind != group.Kind))
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

        if (group.MemberIds.IsEmpty)
        {
            throw new DiagramException($"The overlay group '{group.Id}' has no members.");
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

        if ((end.PortId is { } portId) && (index.FindBoxType(box) is not null) && (index.FindPort(box, portId) is null))
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
            if ((routed.RouteState == link.RouteState) && routed.Route.SequenceEqual(link.Route))
            {
                continue;
            }

            Put(routed);
            Changes.MarkUpdated(link.Id, DiagramChangeParts.Route);
        }
    }

    private void ResizeGroups()
    {
        var tree = new HashSet<string>(StringComparer.Ordinal);
        var walked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in touched)
        {
            var current = elements.GetValueOrDefault(id) as DiagramBox;
            if (current is DiagramGroup { Kind: DiagramGroupKind.Tree })
            {
                tree.Add(current.Id);
            }

            for (var steps = 0; (current?.ParentId is { } parentId) && (steps <= elements.Count); steps++)
            {
                tree.Add(parentId);
                if (!walked.Add(parentId))
                {
                    break;
                }

                current = elements.GetValueOrDefault(parentId) as DiagramBox;
            }
        }

        var resized = new HashSet<string>(StringComparer.Ordinal);
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        if (tree.Count > 0)
        {
            var children = ChildrenMap();
            foreach (var id in tree.OrderByDescending(Depth))
            {
                if (elements.GetValueOrDefault(id) is not DiagramGroup { Kind: DiagramGroupKind.Tree } group)
                {
                    continue;
                }

                var type = index.FindBoxType(group) as DiagramGroupType;
                var padding = type?.Padding ?? DefaultPadding;
                var inner = children.TryGetValue(id, out var list) ? list.Select(x => ((DiagramBox)elements[x]).Bounds).ToList() : [];
                DiagramRect bounds;
                if (type?.Sizing == DiagramGroupSizing.Content)
                {
                    bounds = inner.Count > 0 ? Expand(Union(inner), padding) : group.Bounds with { Width = type.Size.Width, Height = type.Size.Height };
                }
                else
                {
                    var needed = inner.Count > 0 ? Expand(Union(inner), padding) : group.Bounds;
                    bounds = Contains(group.Bounds, needed) ? group.Bounds : Union([group.Bounds, needed]);
                }

                if (Resize(group, bounds))
                {
                    resized.Add(id);
                }
            }
        }

        foreach (var group in groups.ToArray())
        {
            if (group.Kind != DiagramGroupKind.Overlay)
            {
                continue;
            }

            var members = group.MemberIds.Select(elements.GetValueOrDefault).OfType<DiagramBox>().Select(static x => x.Bounds).ToList();
            var padding = (index.FindBoxType(group) as DiagramGroupType)?.Padding ?? DefaultPadding;
            if ((members.Count > 0) && Resize(group, Expand(Union(members), padding)))
            {
                resized.Add(group.Id);
            }
        }

        if (resized.Count > 0)
        {
            Reroute(resized);
        }

        int Depth(string id)
        {
            var chain = new List<string>();
            var current = id;
            int depth;
            while (!depths.TryGetValue(current, out depth))
            {
                chain.Add(current);
                if ((chain.Count > elements.Count) || ((elements.GetValueOrDefault(current) as DiagramBox)?.ParentId is not { } parentId))
                {
                    depth = -1;
                    break;
                }

                current = parentId;
            }

            for (var i = chain.Count - 1; i >= 0; i--)
            {
                depth++;
                depths[chain[i]] = depth;
            }

            return depths[id];
        }
    }

    private bool Resize(DiagramGroup group, DiagramRect bounds)
    {
        if (Near(bounds, group.Bounds))
        {
            return false;
        }

        var resized = group with { Bounds = bounds };
        Put(resized);
        Changes.MarkUpdated(group.Id, BoxParts(group, resized));
        return true;
    }

    private void ReorderGroups()
    {
        var tree = groups.Where(static x => x.Kind == DiagramGroupKind.Tree).ToList();
        var ids = tree.Select(static x => x.Id).ToHashSet(StringComparer.Ordinal);
        var childGroups = new Dictionary<string, List<DiagramGroup>>(StringComparer.Ordinal);
        foreach (var group in tree)
        {
            if ((group.ParentId is { } parentId) && ids.Contains(parentId))
            {
                if (!childGroups.TryGetValue(parentId, out var list))
                {
                    list = [];
                    childGroups[parentId] = list;
                }

                list.Add(group);
            }
        }

        var ordered = new List<DiagramGroup>(groups.Count);
        var placed = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<DiagramGroup>();
        foreach (var root in tree.Where(x => (x.ParentId is null) || !ids.Contains(x.ParentId)))
        {
            stack.Push(root);
            while (stack.Count > 0)
            {
                var group = stack.Pop();
                if (!placed.Add(group.Id))
                {
                    continue;
                }

                ordered.Add(group);
                if (childGroups.TryGetValue(group.Id, out var list))
                {
                    for (var i = list.Count - 1; i >= 0; i--)
                    {
                        stack.Push(list[i]);
                    }
                }
            }
        }

        ordered.AddRange(tree.Where(x => !placed.Contains(x.Id)));
        ordered.AddRange(groups.Where(static x => x.Kind == DiagramGroupKind.Overlay));
        for (var i = 0; i < ordered.Count; i++)
        {
            if (!ReferenceEquals(ordered[i], groups[i]))
            {
                Changes.MarkUpdated(ordered[i].Id, DiagramChangeParts.Order);
            }
        }

        groups.Clear();
        groups.AddRange(ordered);
        groupPositions = null;
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
                Set(nodes, ref nodePositions, node);
                break;
            case DiagramGroup group:
                Set(groups, ref groupPositions, group);
                break;
            case DiagramLink link:
                Set(links, ref linkPositions, link);
                break;
            case DiagramSharedItem item:
                Set(sharedItems, ref sharedItemPositions, item);
                break;
        }
    }
}
