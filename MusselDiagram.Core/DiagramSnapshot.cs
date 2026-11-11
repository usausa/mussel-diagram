namespace MusselDiagram;

using System.Collections.Immutable;

public sealed class DiagramSnapshot
{
    internal DiagramSnapshot(long version, ImmutableDictionary<string, DiagramElement> elements, ImmutableArray<DiagramNode> nodes, ImmutableArray<DiagramGroup> groups, ImmutableArray<DiagramLink> links)
    {
        Version = version;
        Elements = elements;
        Nodes = nodes;
        Groups = groups;
        Links = links;
    }

    public long Version { get; }

    public ImmutableArray<DiagramNode> Nodes { get; }

    public ImmutableArray<DiagramGroup> Groups { get; }

#pragma warning disable CA1721
    public ImmutableArray<DiagramLink> Links { get; }
#pragma warning restore CA1721

    public ImmutableArray<DiagramBox> TopLevel => Index.TopLevel;

    internal static DiagramSnapshot Empty { get; } = new(0, ImmutableDictionary.Create<string, DiagramElement>(StringComparer.Ordinal), [], [], []);

    internal ImmutableDictionary<string, DiagramElement> Elements { get; }

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

    private sealed class Lookup
    {
        public Lookup(DiagramSnapshot snapshot)
        {
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
            }

            foreach (var node in snapshot.Nodes)
            {
                Place(node);
            }

            TopLevel = topLevel.ToImmutable();

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

        public ImmutableArray<DiagramBox> TopLevel { get; }

        private static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
        {
            if (!map.TryGetValue(key, out var list))
            {
                list = [];
                map[key] = list;
            }

            list.Add(value);
        }
    }
}
