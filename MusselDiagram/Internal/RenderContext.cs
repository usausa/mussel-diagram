namespace MusselDiagram.Internal;

internal sealed record MarkerDefinition(string Id, DiagramMarker Kind, string? Color);

internal sealed record IconEntry(string Id, string Reference, DiagramIconSet Set, DiagramIcon Icon);

internal sealed class RenderContext
{
    private readonly Dictionary<string, IconEntry> icons = new(StringComparer.Ordinal);

    private readonly Dictionary<string, IconEntry> iconsById = new(StringComparer.Ordinal);

    private readonly Dictionary<(DiagramMarker Kind, string? Color), string> markerIds = [];

    private readonly List<MarkerDefinition> markers = [];

    private readonly string viewId;

    public RenderContext(SchemaIndex index, string viewId, bool tooltips)
    {
        Index = index;
        Tooltips = tooltips;
        this.viewId = viewId;
        foreach (var set in index.Schema.IconSets)
        {
            foreach (var icon in set.Icons)
            {
                var reference = $"{set.Prefix}:{icon.Id}";
                if (icons.ContainsKey(reference))
                {
                    continue;
                }

                var entry = new IconEntry($"mussel-diagram-{viewId}-i{icons.Count}", reference, set, icon);
                icons[reference] = entry;
                iconsById[entry.Id] = entry;
            }
        }

        var defaultId = $"mussel-diagram-{viewId}-m";
        markerIds[(DiagramMarker.Arrow, null)] = defaultId;
        markers.Add(new MarkerDefinition(defaultId, DiagramMarker.Arrow, null));
        foreach (var type in index.Schema.LinkTypes)
        {
            var color = index.LookOf(type).Stroke;
            ResolveMarker(type.SourceMarker, color);
            ResolveMarker(type.TargetMarker, color);
        }
    }

    public SchemaIndex Index { get; }

    public bool Tooltips { get; }

    public IReadOnlyList<MarkerDefinition> Markers => markers;

    public string? FindIconId(string reference) => icons.GetValueOrDefault(reference)?.Id;

    public IconEntry? FindIcon(string reference) => icons.GetValueOrDefault(reference);

    public IconEntry? FindIconById(string id) => iconsById.GetValueOrDefault(id);

    public string? ResolveMarker(DiagramMarker kind, string? color)
    {
        if (kind == DiagramMarker.None)
        {
            return null;
        }

        if (markerIds.TryGetValue((kind, color), out var id))
        {
            return id;
        }

        id = $"mussel-diagram-{viewId}-m{markers.Count}";
        markerIds[(kind, color)] = id;
        markers.Add(new MarkerDefinition(id, kind, color));
        return id;
    }

    public (string? Source, string? Target) ResolveMarkers(DiagramLink link)
    {
        var type = Index.FindLinkType(link.Type);
        var color = link.Look?.Stroke ?? (type is null ? null : Index.LookOf(type).Stroke);
        return (ResolveMarker(LinkRouting.SourceMarkerOf(type, link.Look), color), ResolveMarker(LinkRouting.TargetMarkerOf(type, link.Look), color));
    }
}
