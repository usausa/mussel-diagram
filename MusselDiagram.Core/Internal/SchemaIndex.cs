namespace MusselDiagram.Internal;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;

internal sealed class SchemaIndex
{
    private static readonly ConditionalWeakTable<DiagramSchema, SchemaIndex> Cache = [];

    private static readonly ConcurrentDictionary<Type, JsonTypeInfo> KnownDataTypes = new();

    private readonly Dictionary<string, DiagramNodeType> nodeTypes = new(StringComparer.Ordinal);

    private readonly Dictionary<string, DiagramGroupType> groupTypes = new(StringComparer.Ordinal);

    private readonly Dictionary<string, DiagramLinkType> linkTypes = new(StringComparer.Ordinal);

    private readonly Dictionary<string, DiagramSharedItemType> sharedItemTypes = new(StringComparer.Ordinal);

    private readonly Dictionary<string, DiagramIcon> icons = new(StringComparer.Ordinal);

    private readonly Dictionary<string, DiagramLook> presets = new(StringComparer.Ordinal);

    private readonly Dictionary<DiagramLook, DiagramLook> looks = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<DiagramBoxType, ImmutableArray<PortPlacement>> typePorts = new(ReferenceEqualityComparer.Instance);

    private readonly ConditionalWeakTable<DiagramBox, StrongBox<ImmutableArray<PortPlacement>>> elementPorts = [];

    private SchemaIndex(DiagramSchema schema)
    {
        Schema = schema;
        foreach (var preset in schema.Presets)
        {
            presets.TryAdd(preset.Name, preset.Look);
        }

        foreach (var type in schema.NodeTypes)
        {
            nodeTypes.TryAdd(type.Name, type);
            typePorts.TryAdd(type, LinkRouting.Place(type.Ports, type));
            AddLook(type.Look);
            Register(type);
        }

        foreach (var type in schema.GroupTypes)
        {
            groupTypes.TryAdd(type.Name, type);
            typePorts.TryAdd(type, LinkRouting.Place(type.Ports, type));
            AddLook(type.Look);
            Register(type);
        }

        foreach (var type in schema.LinkTypes)
        {
            linkTypes.TryAdd(type.Name, type);
            AddLook(type.Look);
            Register(type);
        }

        foreach (var type in schema.SharedItemTypes)
        {
            sharedItemTypes.TryAdd(type.Name, type);
            Register(type);
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

    public IEnumerable<DiagramBoxType> BoxTypes => nodeTypes.Values.Cast<DiagramBoxType>().Concat(groupTypes.Values);

    public IEnumerable<DiagramSharedItemType> SharedItemTypes => sharedItemTypes.Values;

    public static SchemaIndex For(DiagramSchema schema) => Cache.GetValue(schema, static x => new SchemaIndex(x));

    public static JsonTypeInfo? FindDataType(Type type) => KnownDataTypes.GetValueOrDefault(type);

    public DiagramType? FindType(DiagramElement element) => element switch
    {
        DiagramBox box => FindBoxType(box),
        DiagramLink link => FindLinkType(link.Type),
        DiagramSharedItem item => FindSharedItemType(item.Type),
        _ => null
    };

    public DiagramBoxType? FindBoxType(DiagramBox box) => box is DiagramGroup group
        ? (groupTypes.GetValueOrDefault(group.Type) is { } type) && (type.Kind == group.Kind) ? type : null
        : nodeTypes.GetValueOrDefault(box.Type);

    public DiagramBoxType? FindBoxType(string name) =>
        nodeTypes.GetValueOrDefault(name) ?? (DiagramBoxType?)groupTypes.GetValueOrDefault(name);

    public DiagramGroupType? FindGroupType(string name) => groupTypes.GetValueOrDefault(name);

    public DiagramLinkType? FindLinkType(string name) => linkTypes.GetValueOrDefault(name);

    public DiagramSharedItemType? FindSharedItemType(string name) => sharedItemTypes.GetValueOrDefault(name);

    public DiagramIcon? FindIcon(string reference) => icons.GetValueOrDefault(reference);

    public DiagramLook LookOf(DiagramBoxType type) => Resolve(type.Look);

    public DiagramLook LookOf(DiagramLinkType type) => Resolve(type.Look);

    public ImmutableArray<PortPlacement> PortsOf(DiagramBox box)
    {
        var type = FindBoxType(box);
        if (type?.GetPorts is not { } getPorts)
        {
            return (type is not null) && typePorts.TryGetValue(type, out var placements) ? placements : [];
        }

        return elementPorts.GetValue(box, x => new StrongBox<ImmutableArray<PortPlacement>>(LinkRouting.Place(getPorts(x), type))).Value;
    }

    public PortPlacement? FindPort(DiagramBox box, string? portId) =>
        portId is null ? null : PortsOf(box).FirstOrDefault(x => string.Equals(x.Id, portId, StringComparison.Ordinal));

    private DiagramLook Resolve(DiagramLook look) => looks.GetValueOrDefault(look) ?? look;

    private void AddLook(DiagramLook look)
    {
        if (looks.ContainsKey(look))
        {
            return;
        }

        if ((look.Preset is not { } name) || (presets.GetValueOrDefault(name) is not { } preset))
        {
            looks[look] = look;
            return;
        }

        looks[look] = new DiagramLook
        {
            Shape = look.Shape != DiagramShape.Rectangle ? look.Shape : preset.Shape,
            Path = look.Path ?? preset.Path,
            Icon = look.Icon ?? preset.Icon,
            Fill = look.Fill ?? preset.Fill,
            Stroke = look.Stroke ?? preset.Stroke,
            StrokeWidth = look.StrokeWidth ?? preset.StrokeWidth,
            Dash = look.Dash != DiagramDash.Solid ? look.Dash : preset.Dash,
            Preset = name
        };
    }

    private static void Register(DiagramType type)
    {
        if (type.DataType is { } dataType)
        {
            KnownDataTypes[dataType.Type] = dataType;
        }
    }
}
