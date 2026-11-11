namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal sealed class SchemaIndex
{
    private readonly Dictionary<string, DiagramNodeType> nodeTypes = new(StringComparer.Ordinal);

    private readonly Dictionary<string, DiagramGroupType> groupTypes = new(StringComparer.Ordinal);

    private readonly Dictionary<string, DiagramLinkType> linkTypes = new(StringComparer.Ordinal);

    private readonly Dictionary<string, DiagramIcon> icons = new(StringComparer.Ordinal);

    private readonly Dictionary<DiagramBoxType, ImmutableArray<PortPlacement>> ports = new(ReferenceEqualityComparer.Instance);

    public SchemaIndex(DiagramSchema schema)
    {
        Schema = schema;
        foreach (var type in schema.NodeTypes)
        {
            nodeTypes.TryAdd(type.Name, type);
            ports.TryAdd(type, LinkRouting.Place(type.Ports));
        }

        foreach (var type in schema.GroupTypes)
        {
            groupTypes.TryAdd(type.Name, type);
            ports.TryAdd(type, LinkRouting.Place(type.Ports));
        }

        foreach (var type in schema.LinkTypes)
        {
            linkTypes.TryAdd(type.Name, type);
        }

        foreach (var set in schema.IconSets)
        {
            foreach (var icon in set.Icons)
            {
                icons.TryAdd($"{set.Prefix}:{icon.Id}", icon);
            }
        }
    }

    public DiagramSchema Schema { get; }

    public DiagramBoxType? FindBoxType(DiagramBox box) =>
        box is DiagramGroup ? groupTypes.GetValueOrDefault(box.Type) : nodeTypes.GetValueOrDefault(box.Type);

    public DiagramGroupType? FindGroupType(string name) => groupTypes.GetValueOrDefault(name);

    public ImmutableArray<PortPlacement> PortsOf(DiagramBoxType? type) =>
        (type is not null) && ports.TryGetValue(type, out var placements) ? placements : [];

    public PortPlacement? FindPort(DiagramBox box, string? portId) =>
        portId is null ? null : PortsOf(FindBoxType(box)).FirstOrDefault(x => string.Equals(x.Id, portId, StringComparison.Ordinal));

    public DiagramLinkType? FindLinkType(string name) => linkTypes.GetValueOrDefault(name);

    public DiagramIcon? FindIcon(string reference) => icons.GetValueOrDefault(reference);
}
