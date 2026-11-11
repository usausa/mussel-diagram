namespace MusselDiagram;

using System.Collections.Immutable;

using MusselDiagram.Internal;

public sealed class DiagramSnapshot
{
    internal DiagramSnapshot(
        long version,
        ImmutableDictionary<string, DiagramElement> elements,
        ImmutableArray<DiagramNode> nodes,
        ImmutableArray<DiagramGroup> groups,
        ImmutableArray<DiagramLink> links,
        ImmutableArray<DiagramSharedItem> sharedItems,
        ObstacleIndex? obstacles = null)
    {
        CachedObstacles = obstacles;
        Version = version;
        Elements = elements;
        Nodes = nodes;
        Groups = groups;
        Links = links;
        SharedItems = sharedItems;
    }

    public long Version { get; }

    public ImmutableArray<DiagramNode> Nodes { get; }

    public ImmutableArray<DiagramGroup> Groups { get; }

#pragma warning disable CA1721
    public ImmutableArray<DiagramLink> Links { get; }
#pragma warning restore CA1721

    public ImmutableArray<DiagramSharedItem> SharedItems { get; }

    public ImmutableArray<DiagramBox> TopLevel => Index.TopLevel;

    public ImmutableArray<DiagramBox> Cycles => Index.Cycles;

    internal static DiagramSnapshot Empty { get; } = new(0, ImmutableDictionary.Create<string, DiagramElement>(StringComparer.Ordinal), [], [], [], []);

    internal ImmutableDictionary<string, DiagramElement> Elements { get; }

    internal ObstacleIndex Obstacles => CachedObstacles ??= new ObstacleIndex(Nodes);

    internal ObstacleIndex? CachedObstacles { get; private set; }

    private Lookup Index => field ??= new Lookup(this);

    public DiagramElement? Find(string id) => Elements.GetValueOrDefault(id);

    public bool Contains(string id) => Elements.ContainsKey(id);

    public ImmutableArray<DiagramLink> GetLinks(string id, DiagramLinkDirection direction = DiagramLinkDirection.Both, string? portId = null)
    {
        if (!Index.LinksByElement.TryGetValue(id, out var links))
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<DiagramLink>();
        foreach (var link in links)
        {
            var outgoing = string.Equals(link.Source.ElementId, id, StringComparison.Ordinal) && ((portId is null) || string.Equals(link.Source.PortId, portId, StringComparison.Ordinal));
            var incoming = string.Equals(link.Target.ElementId, id, StringComparison.Ordinal) && ((portId is null) || string.Equals(link.Target.PortId, portId, StringComparison.Ordinal));
            var matched = direction switch
            {
                DiagramLinkDirection.Out => outgoing,
                DiagramLinkDirection.In => incoming,
                _ => outgoing || incoming
            };
            if (matched)
            {
                builder.Add(link);
            }
        }

        return builder.ToImmutable();
    }

    public ImmutableArray<DiagramBox> GetPredecessors(string id) => Neighbors(id, false);

    public ImmutableArray<DiagramBox> GetSuccessors(string id) => Neighbors(id, true);

    public ImmutableArray<DiagramBox> GetUpstream(string id) => Reach([id], false, false);

    public ImmutableArray<DiagramBox> GetDownstream(string id) => Reach([id], true, false);

    public ImmutableArray<DiagramBox> GetUnreachable(IEnumerable<string> startIds)
    {
        var reached = Reach(startIds, true, true).Select(static x => x.Id).ToHashSet(StringComparer.Ordinal);
        return [.. Index.Boxes.Where(x => !reached.Contains(x.Id) && ((x is DiagramNode) || Index.LinksByElement.ContainsKey(x.Id)))];
    }

    public DiagramBox? GetParent(string id) =>
        (Find(id) is DiagramBox { ParentId: { } parentId }) ? Find(parentId) as DiagramBox : null;

    public ImmutableArray<DiagramBox> GetChildren(string id) =>
        Index.ChildrenByParent.TryGetValue(id, out var children) ? [.. children] : [];

    public ImmutableArray<DiagramBox> GetDescendants(string id)
    {
        var builder = ImmutableArray.CreateBuilder<DiagramBox>();
        var stack = new Stack<string>();
        stack.Push(id);
        while (stack.Count > 0)
        {
            if (!Index.ChildrenByParent.TryGetValue(stack.Pop(), out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                builder.Add(child);
                stack.Push(child.Id);
            }
        }

        return builder.ToImmutable();
    }

    public ImmutableArray<DiagramBox> GetAncestors(string id)
    {
        var builder = ImmutableArray.CreateBuilder<DiagramBox>();
        var current = GetParent(id);
        while ((current is not null) && (builder.Count <= Elements.Count))
        {
            builder.Add(current);
            current = GetParent(current.Id);
        }

        return builder.ToImmutable();
    }

    public ImmutableArray<DiagramBox> GetMembers(string groupId) =>
        Find(groupId) is DiagramGroup { Kind: DiagramGroupKind.Overlay } group ? [.. group.MemberIds.Select(Find).OfType<DiagramBox>()] : [];

    public ImmutableArray<DiagramGroup> GetOverlayGroups(string id) =>
        Index.OverlaysByMember.TryGetValue(id, out var groups) ? [.. groups] : [];

    public ImmutableArray<DiagramElement> GetReferrers(string sharedItemId, DiagramSchema schema)
    {
        var index = SchemaIndex.For(schema);
        return [.. AllElements().Where(x => DiagramRules.References(x, index).Contains(sharedItemId, StringComparer.Ordinal))];
    }

    public ImmutableArray<DiagramSharedItem> GetUnusedSharedItems(DiagramSchema schema)
    {
        var index = SchemaIndex.For(schema);
        var used = AllElements().SelectMany(x => DiagramRules.References(x, index)).ToHashSet(StringComparer.Ordinal);
        return [.. SharedItems.Where(x => !used.Contains(x.Id))];
    }

    public ImmutableArray<DiagramIssue> Validate(DiagramSchema schema) => DiagramValidator.Validate(this, SchemaIndex.For(schema));

    internal DiagramSnapshot WithVersion(long version) => new(version, Elements, Nodes, Groups, Links, SharedItems, CachedObstacles);

    private IEnumerable<DiagramElement> AllElements() =>
        Groups.Cast<DiagramElement>().Concat(Nodes).Concat(Links).Concat(SharedItems);

    private ImmutableArray<DiagramBox> Neighbors(string id, bool forward)
    {
        if (!Index.LinksByElement.TryGetValue(id, out var links))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var builder = ImmutableArray.CreateBuilder<DiagramBox>();
        foreach (var link in links)
        {
            var (from, to) = forward ? (link.Source.ElementId, link.Target.ElementId) : (link.Target.ElementId, link.Source.ElementId);
            if (string.Equals(from, id, StringComparison.Ordinal) && seen.Add(to) && (Find(to) is DiagramBox box))
            {
                builder.Add(box);
            }
        }

        return builder.ToImmutable();
    }

    private ImmutableArray<DiagramBox> Reach(IEnumerable<string> startIds, bool forward, bool includeStarts)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        var builder = ImmutableArray.CreateBuilder<DiagramBox>();
        foreach (var id in startIds)
        {
            if (seen.Add(id))
            {
                queue.Enqueue(id);
                if (includeStarts && (Find(id) is DiagramBox start))
                {
                    builder.Add(start);
                }
            }
        }

        while (queue.Count > 0)
        {
            foreach (var next in Neighbors(queue.Dequeue(), forward))
            {
                if (seen.Add(next.Id))
                {
                    builder.Add(next);
                    queue.Enqueue(next.Id);
                }
            }
        }

        return builder.ToImmutable();
    }

    private sealed class Lookup
    {
        private readonly DiagramSnapshot snapshot;

        public Lookup(DiagramSnapshot snapshot)
        {
            this.snapshot = snapshot;
            foreach (var link in snapshot.Links)
            {
                Add(LinksByElement, link.Source.ElementId, link);
                if (!string.Equals(link.Source.ElementId, link.Target.ElementId, StringComparison.Ordinal))
                {
                    Add(LinksByElement, link.Target.ElementId, link);
                }
            }

            var topLevel = ImmutableArray.CreateBuilder<DiagramBox>();
            foreach (var group in snapshot.Groups)
            {
                Place(group);
                if (group.Kind == DiagramGroupKind.Overlay)
                {
                    foreach (var memberId in group.MemberIds)
                    {
                        Add(OverlaysByMember, memberId, group);
                    }
                }
            }

            foreach (var node in snapshot.Nodes)
            {
                Place(node);
            }

            TopLevel = topLevel.ToImmutable();
            Boxes = [.. snapshot.Groups, .. snapshot.Nodes];

            void Place(DiagramBox box)
            {
                if (box.ParentId is null)
                {
                    topLevel.Add(box);
                }
                else
                {
                    Add(ChildrenByParent, box.ParentId, box);
                }
            }
        }

        public Dictionary<string, List<DiagramLink>> LinksByElement { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<DiagramBox>> ChildrenByParent { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<DiagramGroup>> OverlaysByMember { get; } = new(StringComparer.Ordinal);

        public ImmutableArray<DiagramBox> TopLevel { get; }

        public ImmutableArray<DiagramBox> Boxes { get; }

        public ImmutableArray<DiagramBox> Cycles
        {
            get
            {
                if (field.IsDefault)
                {
                    field = FindCycles(snapshot, Boxes);
                }

                return field;
            }
        }

        private static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
        {
            if (!map.TryGetValue(key, out var list))
            {
                list = [];
                map[key] = list;
            }

            list.Add(value);
        }

        private static ImmutableArray<DiagramBox> FindCycles(DiagramSnapshot snapshot, ImmutableArray<DiagramBox> boxes)
        {
            if (snapshot.Links.IsEmpty)
            {
                return [];
            }

            var positions = new Dictionary<string, int>(boxes.Length, StringComparer.Ordinal);
            for (var i = 0; i < boxes.Length; i++)
            {
                positions[boxes[i].Id] = i;
            }

            var edges = new List<int>?[boxes.Length];
            var inCycle = new bool[boxes.Length];
            foreach (var link in snapshot.Links)
            {
                if (!positions.TryGetValue(link.Source.ElementId, out var source) || !positions.TryGetValue(link.Target.ElementId, out var target))
                {
                    continue;
                }

                if (source == target)
                {
                    inCycle[source] = true;
                }
                else
                {
                    (edges[source] ??= []).Add(target);
                }
            }

            var order = new int[boxes.Length];
            Array.Fill(order, -1);
            var low = new int[boxes.Length];
            var onStack = new bool[boxes.Length];
            var stack = new Stack<int>();
            var work = new Stack<(int Node, int Edge)>();
            var counter = 0;
            for (var start = 0; start < boxes.Length; start++)
            {
                if (order[start] >= 0)
                {
                    continue;
                }

                Enter(start);
                while (work.Count > 0)
                {
                    var (node, edge) = work.Pop();
                    var next = edges[node];
                    if ((next is not null) && (edge < next.Count))
                    {
                        work.Push((node, edge + 1));
                        var target = next[edge];
                        if (order[target] < 0)
                        {
                            Enter(target);
                        }
                        else if (onStack[target])
                        {
                            low[node] = Math.Min(low[node], order[target]);
                        }

                        continue;
                    }

                    if (work.Count > 0)
                    {
                        var parent = work.Peek().Node;
                        low[parent] = Math.Min(low[parent], low[node]);
                    }

                    if (low[node] != order[node])
                    {
                        continue;
                    }

                    var count = 0;
                    int member;
                    var first = -1;
                    do
                    {
                        member = stack.Pop();
                        onStack[member] = false;
                        count++;
                        if (count == 1)
                        {
                            first = member;
                        }
                        else
                        {
                            inCycle[member] = true;
                            inCycle[first] = true;
                        }
                    }
                    while (member != node);
                }
            }

            var builder = ImmutableArray.CreateBuilder<DiagramBox>();
            for (var i = 0; i < boxes.Length; i++)
            {
                if (inCycle[i])
                {
                    builder.Add(boxes[i]);
                }
            }

            return builder.ToImmutable();

            void Enter(int node)
            {
                order[node] = counter;
                low[node] = counter;
                counter++;
                stack.Push(node);
                onStack[node] = true;
                work.Push((node, 0));
            }
        }
    }
}
