namespace MusselDiagram.Internal;

using System.Collections.Immutable;
using System.Text.Json;

internal sealed class SnapshotEditor
{
    private const double Tolerance = 1e-6;

    private static readonly DiagramThickness DefaultPadding = new DiagramGroupType { Name = string.Empty }.Padding;

    private readonly SchemaIndex index;

    private readonly DiagramSnapshot source;

    private readonly ImmutableDictionary<string, DiagramElement>.Builder elements;

    private readonly ElementList<DiagramNode> nodes;

    private readonly ElementList<DiagramGroup> groups;

    private readonly ElementList<DiagramLink> links;

    private readonly ElementList<DiagramSharedItem> sharedItems;

    private readonly HashSet<string> touched = new(StringComparer.Ordinal);

    private readonly HashSet<string> rerouted = new(StringComparer.Ordinal);

    private readonly List<DiagramRect> dirty = [];

    private DiagramRect? dirtyBounds;

    private readonly List<PendingRoute> pendingRoutes = [];

    private readonly HashSet<string> deferred = new(StringComparer.Ordinal);

    private ObstacleIndex? obstacles;

    private bool nodesChanged;

    private bool reorder;

    public SnapshotEditor(DiagramSnapshot source, SchemaIndex index)
    {
        this.source = source;
        this.index = index;
        elements = source.Elements.ToBuilder();
        nodes = new ElementList<DiagramNode>(source.Nodes);
        groups = new ElementList<DiagramGroup>(source.Groups);
        links = new ElementList<DiagramLink>(source.Links);
        sharedItems = new ElementList<DiagramSharedItem>(source.SharedItems);
    }

    public bool SkipKindCheck { get; init; }

    public bool SkipCycleCheck { get; init; }

    public bool KeepMissingPorts { get; init; }

    public bool SkipRoutes { get; init; }

    public bool SkipAppRouter { get; init; }

    public RouteBudget? Budget { get; init; }

    public IReadOnlyCollection<string> DeferredLinks => deferred;

    public ChangeSet Changes { get; } = new();

    public IReadOnlyList<PendingRoute> PendingRoutes => pendingRoutes;

    public DiagramElement? Find(string id) => elements.GetValueOrDefault(id);

    private ObstacleIndex Obstacles => obstacles ??= nodesChanged ? new ObstacleIndex(nodes.Items) : source.Obstacles;

    public DiagramSnapshot Build(long version) =>
        new(version, elements.ToImmutable(), nodes.ToImmutable(), groups.ToImmutable(), links.ToImmutable(), sharedItems.ToImmutable(), nodesChanged ? obstacles : source.CachedObstacles);

    public void Complete()
    {
        if (reorder)
        {
            ReorderGroups();
        }

        ResizeGroups();
        if (!SkipRoutes)
        {
            RerouteLinks();
        }

        touched.Clear();
        rerouted.Clear();
        dirty.Clear();
        reorder = false;
    }

    public void Add(DiagramElement element)
    {
        Add(element, true);
        Changes.Targets.Add(element.Id);
    }

    public void AddWithRoute(DiagramLink link)
    {
        Changes.Targets.Add(link.Id);
        if ((link.Route.Length >= 2) &&
            (elements.GetValueOrDefault(link.Source.ElementId) is DiagramBox sourceBox) &&
            (elements.GetValueOrDefault(link.Target.ElementId) is DiagramBox targetBox))
        {
            var (start, end) = LinkRouting.Anchors(sourceBox.Bounds, index.FindPort(sourceBox, link.Source.PortId), targetBox.Bounds, index.FindPort(targetBox, link.Target.PortId));
            if (Near(start.Position, link.Route[0]) && Near(end.Position, link.Route[^1]))
            {
                Add(link with { RouteState = DiagramRouteState.Routed, RouteError = null }, false);
                return;
            }
        }

        Add(link, true);
    }

    public void Resize(string id, DiagramRect bounds)
    {
        if (elements.GetValueOrDefault(id) is not DiagramBox box)
        {
            throw new DiagramException($"The element '{id}' is not a node or a group.");
        }

        if ((box.Bounds == bounds) || (box is DiagramGroup { Kind: DiagramGroupKind.Overlay }) || (index.FindBoxType(box) is DiagramGroupType { Sizing: DiagramGroupSizing.Content }))
        {
            return;
        }

        Replace(box with { Bounds = bounds });
    }

    public void Reconnect(string linkId, DiagramLinkEndSide side, DiagramLinkEnd end)
    {
        if (elements.GetValueOrDefault(linkId) is not DiagramLink link)
        {
            throw new DiagramException($"The element '{linkId}' is not a link.");
        }

        Replace(side == DiagramLinkEndSide.Source ? link with { Source = end } : link with { Target = end });
    }

    public List<string> RouteDeferred(IEnumerable<string> ids)
    {
        var taken = new List<string>();
        foreach (var id in ids)
        {
            if ((taken.Count > 0) && (Budget is { Exceeded: true }))
            {
                break;
            }

            taken.Add(id);
            if (elements.GetValueOrDefault(id) is DiagramLink { RouteState: DiagramRouteState.Provisional } link)
            {
                Reroute(link, true);
            }
        }

        return taken;
    }

    public void RequestRoutes(IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            if (elements.GetValueOrDefault(id) is DiagramLink { RouteState: DiagramRouteState.Provisional } link)
            {
                Reroute(link, false);
            }
        }
    }

    public void ApplyRoutes(IEnumerable<(DiagramLink Original, DiagramLink Routed, ObstacleIndex Obstacles)> results)
    {
        foreach (var (original, routed, used) in results)
        {
            if (!ReferenceEquals(elements.GetValueOrDefault(original.Id), original))
            {
                continue;
            }

            if (!ReferenceEquals(used, Obstacles) && !Avoids(routed))
            {
                deferred.Add(original.Id);
                continue;
            }

            Put(routed);
            Changes.MarkUpdated(routed.Id, DiagramChangeParts.Route);
        }
    }

    private bool Avoids(DiagramLink link)
    {
        var sourceBox = (DiagramBox)elements[link.Source.ElementId];
        var targetBox = (DiagramBox)elements[link.Target.ElementId];
        var margin = index.FindLinkType(link.Type)?.ObstacleMargin ?? LinkRouting.DefaultObstacleMargin;
        return OrthogonalRouter.Avoids(link.Route, Obstacles, sourceBox.Id, targetBox.Id, margin, sourceBox.Bounds, targetBox.Bounds);
    }

    public void CompleteRoute(PendingRoute route, Task<DiagramRouteResult> task)
    {
        if (elements.GetValueOrDefault(route.LinkId) is not DiagramLink link)
        {
            return;
        }

        var current = Prepare(link);
        if ((current.Request is not { } request) || !SameRequest(request, route.Request))
        {
            Put(Track(RouteOf(link)));
            Changes.MarkUpdated(link.Id, DiagramChangeParts.Route);
            return;
        }

        var result = task.IsCompletedSuccessfully ? task.Result : new DiagramRouteResult { Error = task.Exception?.GetBaseException().Message ?? "The route was canceled." };
        Put(Routed(link, result, current.Simple));
        Changes.MarkUpdated(link.Id, DiagramChangeParts.Route);
    }

    private void Add(DiagramElement element, bool route)
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
                groups.Add(group);
                reorder |= group.ParentId is not null;
                touched.Add(group.Id);
                break;
            case DiagramNode node:
                ValidateBox(node);
                elements[node.Id] = node;
                nodes.Add(node);
                NodesChanged();
                touched.Add(node.Id);
                dirty.Add(node.Bounds);
                break;
            case DiagramLink link:
                ValidateLink(link);
                var routed = route ? Track(RouteOf(link)) : link;
                elements[routed.Id] = routed;
                links.Add(routed);
                break;
            case DiagramSharedItem item:
                elements[item.Id] = item;
                sharedItems.Add(item);
                break;
            default:
                throw new DiagramException($"The element '{element.Id}' is not supported.");
        }

        Changes.MarkAdded(element.Id);
    }

    public void Delete(IEnumerable<string> ids)
    {
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var children = ChildrenMap();
        foreach (var id in ids)
        {
            Changes.Targets.Add(id);
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

        Changes.Targets.Add(element.Id);

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
                if ((box is DiagramNode) && ((boxParts & (DiagramChangeParts.Position | DiagramChangeParts.Size)) != 0))
                {
                    dirty.Add(before.Bounds);
                    dirty.Add(box.Bounds);
                }

                RemoveLinksToLostPorts(before, box);
                rerouted.Add(box.Id);
                break;
            case DiagramLink link:
                ValidateLink(link);
                var old = (DiagramLink)current;
                var linkParts = LinkParts(old, link);
                var reroute = ((linkParts & DiagramChangeParts.Ends) != 0) || !string.Equals(old.Type, link.Type, StringComparison.Ordinal);
                Put(reroute ? Track(RouteOf(link)) : link);
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
        if (!double.IsFinite(offset.X) || !double.IsFinite(offset.Y))
        {
            throw new DiagramException("The offset is not a pair of finite numbers.");
        }

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
                    Changes.Targets.Add(id);
                    foreach (var member in overlay.MemberIds)
                    {
                        Collect(member);
                    }

                    break;
                case DiagramBox box:
                    Changes.Targets.Add(id);
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
            var moved = box with { Bounds = box.Bounds.Offset(offset) };
            Put(moved);
            Changes.MarkUpdated(id, DiagramChangeParts.Position);
            touched.Add(id);
            if (box is DiagramNode)
            {
                dirty.Add(box.Bounds);
                dirty.Add(moved.Bounds);
            }
        }

        rerouted.UnionWith(moving);

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
            Changes.Targets.Add(id);
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

        Changes.Targets.Add(groupId);

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

    private static bool Near(DiagramPoint x, DiagramPoint y) => Near(x.X, y.X) && Near(x.Y, y.Y);

    private static bool Near(DiagramRect x, DiagramRect y) =>
        Near(x.X, y.X) && Near(x.Y, y.Y) && Near(x.Width, y.Width) && Near(x.Height, y.Height);

    private void Remove(HashSet<string> targets)
    {
        foreach (var group in groups.Items.ToArray())
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

        foreach (var link in links.Items)
        {
            if (targets.Contains(link.Source.ElementId) || targets.Contains(link.Target.ElementId))
            {
                targets.Add(link.Id);
            }
        }

        foreach (var id in targets)
        {
            var removed = elements.GetValueOrDefault(id);
            if (removed is DiagramNode node)
            {
                dirty.Add(node.Bounds);
            }

            if ((removed is DiagramBox { ParentId: { } parentId }) && !targets.Contains(parentId))
            {
                touched.Add(parentId);
            }
        }

        if (nodes.RemoveAll(x => targets.Contains(x.Id)) > 0)
        {
            NodesChanged();
        }

        groups.RemoveAll(x => targets.Contains(x.Id));
        links.RemoveAll(x => targets.Contains(x.Id));
        sharedItems.RemoveAll(x => targets.Contains(x.Id));
        foreach (var id in targets)
        {
            elements.Remove(id);
            touched.Remove(id);
            deferred.Remove(id);
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
        foreach (var link in links.Items)
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

        if (!KeepMissingPorts && (end.PortId is { } portId) && (index.FindBoxType(box) is not null) && (index.FindPort(box, portId) is null))
        {
            throw new DiagramException($"The port '{portId}' of '{box.Id}' does not exist.");
        }
    }

    private static DiagramLink Routed(DiagramLink link, DiagramRouteResult result, ImmutableArray<DiagramPoint> simple) =>
        (result.Error is null) && (result.Points.Length >= 2)
            ? link with { Route = result.Points, RouteState = DiagramRouteState.Routed, RouteError = null }
            : link with { Route = simple, RouteState = DiagramRouteState.Failed, RouteError = result.Error ?? "The router returned no route." };

    private static bool SameRequest(DiagramRouteRequest x, DiagramRouteRequest y) =>
        (x.Source == y.Source) && (x.Target == y.Target) && ReferenceEquals(x.LinkType, y.LinkType) && x.Obstacles.SequenceEqual(y.Obstacles);

    private DiagramLink Track((DiagramLink Link, PendingRoute? Pending) routed)
    {
        if (routed.Pending is { } pending)
        {
            pendingRoutes.Add(pending);
        }

        return routed.Link;
    }

    private (LinkAnchor Start, LinkAnchor End, DiagramBox Source, DiagramBox Target, DiagramLinkType? Type, IDiagramRouter? Router, ImmutableArray<DiagramPoint> Simple, DiagramRouteRequest? Request) Prepare(DiagramLink link)
    {
        var sourceBox = (DiagramBox)elements[link.Source.ElementId];
        var targetBox = (DiagramBox)elements[link.Target.ElementId];
        var type = index.FindLinkType(link.Type);
        var (start, end) = LinkRouting.Anchors(sourceBox.Bounds, index.FindPort(sourceBox, link.Source.PortId), targetBox.Bounds, index.FindPort(targetBox, link.Target.PortId));
        var router = type?.Router ?? index.Schema.Router;
        DiagramRouteRequest? request = null;
        if ((router is not null) && !SkipAppRouter)
        {
            var obstacleList = new List<DiagramRect>();
            Obstacles.All(sourceBox.Id, targetBox.Id, obstacleList);
            request = new DiagramRouteRequest
            {
                Source = new DiagramRouteEnd { Position = start.Position, Direction = start.Side },
                Target = new DiagramRouteEnd { Position = end.Position, Direction = end.Side },
                Obstacles = [.. obstacleList],
                LinkType = type,
                GridSize = index.Schema.GridSize
            };
        }

        return (start, end, sourceBox, targetBox, type, router, LinkRouting.Simple(start, end, type, index.Schema.GridSize), request);
    }

    private (DiagramLink Link, PendingRoute? Pending) RouteOf(DiagramLink link, bool force = false)
    {
        var prepared = Prepare(link);
        if (prepared.Router is not { } router)
        {
            var type = prepared.Type;
            if (!force && (Budget is { Exceeded: true }) && ((type?.Form ?? DiagramLineForm.Orthogonal) == DiagramLineForm.Orthogonal))
            {
                var clear = OrthogonalRouter.Clear(
                    prepared.Start,
                    prepared.End,
                    type?.StubLength ?? LinkRouting.DefaultStubLength,
                    type?.ObstacleMargin ?? LinkRouting.DefaultObstacleMargin,
                    Obstacles,
                    prepared.Source.Id,
                    prepared.Target.Id,
                    prepared.Source.Bounds,
                    prepared.Target.Bounds,
                    index.Schema.GridSize);
                if (clear is null)
                {
                    deferred.Add(link.Id);
                    return (link with { Route = prepared.Simple, RouteState = DiagramRouteState.Provisional, RouteError = null }, null);
                }

                deferred.Remove(link.Id);
                return (link with { Route = clear.Value, RouteState = DiagramRouteState.Routed, RouteError = null }, null);
            }

            deferred.Remove(link.Id);
            var route = LinkRouting.Route(prepared.Start, prepared.End, type, Obstacles, prepared.Source, prepared.Target, index.Schema.GridSize);
            return (link with { Route = route, RouteState = DiagramRouteState.Routed, RouteError = null }, null);
        }

        var provisional = link with { Route = prepared.Simple, RouteState = DiagramRouteState.Provisional, RouteError = null };
        if (prepared.Request is not { } request)
        {
            return (provisional, null);
        }

        CancellationTokenSource? cancellation = null;
        try
        {
            cancellation = new CancellationTokenSource();
            var task = router.RouteAsync(request, cancellation.Token);
            if (task.IsCompleted)
            {
                return (Routed(link, task.GetAwaiter().GetResult(), prepared.Simple), null);
            }

            var pending = new PendingRoute(link.Id, request, task.AsTask(), cancellation);
            cancellation = null;
            return (provisional, pending);
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    private void RerouteLinks()
    {
        if ((rerouted.Count == 0) && (dirty.Count == 0))
        {
            return;
        }

        dirtyBounds = null;
        foreach (var rect in dirty)
        {
            dirtyBounds = dirtyBounds is { } bounds ? Union([bounds, rect]) : rect;
        }

        foreach (var link in links.Items.ToArray())
        {
            if (!rerouted.Contains(link.Source.ElementId) && !rerouted.Contains(link.Target.ElementId) && !CrossesDirty(link))
            {
                continue;
            }

            Reroute(link, false);
        }
    }

    private void Reroute(DiagramLink link, bool force)
    {
        var routed = Track(RouteOf(link, force));
        if ((routed.RouteState == link.RouteState) && routed.Route.SequenceEqual(link.Route))
        {
            return;
        }

        Put(routed);
        Changes.MarkUpdated(link.Id, DiagramChangeParts.Route);
    }

    private static bool OverlapsRoute(ImmutableArray<DiagramPoint> route, DiagramRect area)
    {
        if (route.IsEmpty)
        {
            return false;
        }

        var left = double.PositiveInfinity;
        var top = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        var bottom = double.NegativeInfinity;
        foreach (var point in route)
        {
            left = Math.Min(left, point.X);
            top = Math.Min(top, point.Y);
            right = Math.Max(right, point.X);
            bottom = Math.Max(bottom, point.Y);
        }

        return (right >= area.X) && (left <= area.Right) && (bottom >= area.Y) && (top <= area.Bottom);
    }

    private void NodesChanged()
    {
        nodesChanged = true;
        obstacles = null;
    }

    private bool CrossesDirty(DiagramLink link)
    {
        if (dirty.Count == 0)
        {
            return false;
        }

        var margin = (index.FindLinkType(link.Type)?.ObstacleMargin ?? LinkRouting.DefaultObstacleMargin) + 1;
        if ((dirtyBounds is not { } bounds) || !OverlapsRoute(link.Route, new DiagramRect(bounds.X - margin, bounds.Y - margin, bounds.Width + (margin * 2), bounds.Height + (margin * 2))))
        {
            return false;
        }

        foreach (var rect in dirty)
        {
            if (OrthogonalRouter.Crosses(link.Route, new DiagramRect(rect.X - margin, rect.Y - margin, rect.Width + (margin * 2), rect.Height + (margin * 2))))
            {
                return true;
            }
        }

        return false;
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

                if (FitGroup(group, bounds))
                {
                    resized.Add(id);
                }
            }
        }

        foreach (var group in groups.Items.ToArray())
        {
            if (group.Kind != DiagramGroupKind.Overlay)
            {
                continue;
            }

            var members = group.MemberIds.Select(elements.GetValueOrDefault).OfType<DiagramBox>().Select(static x => x.Bounds).ToList();
            var padding = (index.FindBoxType(group) as DiagramGroupType)?.Padding ?? DefaultPadding;
            if ((members.Count > 0) && FitGroup(group, Expand(Union(members), padding)))
            {
                resized.Add(group.Id);
            }
        }

        rerouted.UnionWith(resized);

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

    private bool FitGroup(DiagramGroup group, DiagramRect bounds)
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

    public void Reorder(IEnumerable<string> ids, OrderMove move)
    {
        var targets = ids.ToHashSet(StringComparer.Ordinal);
        Changes.Targets.UnionWith(targets);
        Shift(nodes, targets, move, static _ => string.Empty);
        Shift(links, targets, move, static _ => string.Empty);
        if (!groups.Items.Any(x => targets.Contains(x.Id)))
        {
            return;
        }

        var original = groups.Items.ToArray();
        var shifted = Rearrange(original, targets, move, static x => x.Kind == DiagramGroupKind.Overlay ? null : x.ParentId ?? string.Empty);
        var final = TreeOrder(shifted);
        for (var i = 0; i < final.Count; i++)
        {
            if (!ReferenceEquals(final[i], original[i]))
            {
                Changes.MarkUpdated(final[i].Id, DiagramChangeParts.Order);
            }
        }

        groups.Reset(final);
    }

    private static List<T> Rearrange<T>(IReadOnlyList<T> items, HashSet<string> targets, OrderMove move, Func<T, string?> sibling)
        where T : DiagramElement
    {
        if (move == OrderMove.Front)
        {
            return [.. items.Where(x => !targets.Contains(x.Id)), .. items.Where(x => targets.Contains(x.Id))];
        }

        if (move == OrderMove.Back)
        {
            return [.. items.Where(x => targets.Contains(x.Id)), .. items.Where(x => !targets.Contains(x.Id))];
        }

        var order = items.ToList();
        var step = move == OrderMove.Forward ? 1 : -1;
        var start = move == OrderMove.Forward ? order.Count - 1 : 0;
        for (var i = start; (i >= 0) && (i < order.Count); i -= step)
        {
            if (!targets.Contains(order[i].Id))
            {
                continue;
            }

            var key = sibling(order[i]);
            var j = i + step;
            while ((j >= 0) && (j < order.Count) && !string.Equals(sibling(order[j]), key, StringComparison.Ordinal))
            {
                j += step;
            }

            if ((j >= 0) && (j < order.Count) && !targets.Contains(order[j].Id))
            {
                (order[i], order[j]) = (order[j], order[i]);
            }
        }

        return order;
    }

    private void Shift<T>(ElementList<T> list, HashSet<string> targets, OrderMove move, Func<T, string?> sibling)
        where T : DiagramElement
    {
        if (!list.Items.Any(x => targets.Contains(x.Id)))
        {
            return;
        }

        var original = list.Items.ToArray();
        var order = Rearrange(original, targets, move, sibling);
        for (var i = 0; i < order.Count; i++)
        {
            if (!ReferenceEquals(order[i], original[i]))
            {
                Changes.MarkUpdated(order[i].Id, DiagramChangeParts.Order);
            }
        }

        list.Reset(order);
    }

    private static List<DiagramGroup> TreeOrder(IReadOnlyList<DiagramGroup> groups)
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
        return ordered;
    }

    private void ReorderGroups()
    {
        var ordered = TreeOrder(groups.Items);
        for (var i = 0; i < ordered.Count; i++)
        {
            if (!ReferenceEquals(ordered[i], groups[i]))
            {
                Changes.MarkUpdated(ordered[i].Id, DiagramChangeParts.Order);
            }
        }

        groups.Reset(ordered);
    }

    private Dictionary<string, List<string>> ChildrenMap()
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var box in groups.Items.Cast<DiagramBox>().Concat(nodes.Items))
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
                nodes.Set(node);
                NodesChanged();
                break;
            case DiagramGroup group:
                groups.Set(group);
                break;
            case DiagramLink link:
                links.Set(link);
                break;
            case DiagramSharedItem item:
                sharedItems.Set(item);
                break;
        }
    }

    private sealed class ElementList<T>(ImmutableArray<T> source)
        where T : DiagramElement
    {
        private List<T>? items;

        private Dictionary<string, int>? positions;

        public IReadOnlyList<T> Items => items ?? (IReadOnlyList<T>)source;

        public int Count => items?.Count ?? source.Length;

        public T this[int index] => items is null ? source[index] : items[index];

        public void Add(T element)
        {
            positions?.Add(element.Id, Count);
            Edit().Add(element);
        }

        public void Set(T element)
        {
            if (positions is null)
            {
                positions = new Dictionary<string, int>(Count, StringComparer.Ordinal);
                for (var i = 0; i < Count; i++)
                {
                    positions[this[i].Id] = i;
                }
            }

            Edit()[positions[element.Id]] = element;
        }

        public int RemoveAll(Predicate<T> match)
        {
            if (!Items.Any(x => match(x)))
            {
                return 0;
            }

            positions = null;
            return Edit().RemoveAll(match);
        }

        public void Reset(IEnumerable<T> ordered)
        {
            var list = Edit();
            list.Clear();
            list.AddRange(ordered);
            positions = null;
        }

        public ImmutableArray<T> ToImmutable() => items is null ? source : [.. items];

        private List<T> Edit() => items ??= [.. source];
    }
}

internal sealed record PendingRoute(string LinkId, DiagramRouteRequest Request, Task<DiagramRouteResult> Task, CancellationTokenSource Cancellation);
