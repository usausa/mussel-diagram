namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal sealed record MarkerDefinition(string Id, DiagramMarker Kind, string? Color);

internal sealed record IconEntry(string Id, string Reference, DiagramIconSet Set, DiagramIcon Icon);

internal sealed class RenderContext
{
    private readonly Dictionary<string, IconEntry> icons = new(StringComparer.Ordinal);

    private readonly Dictionary<string, IconEntry> iconsById = new(StringComparer.Ordinal);

    private readonly Dictionary<(string Type, DiagramMarker Kind), string> markerIds = [];

    public RenderContext(SchemaIndex index, string viewId, bool tooltips)
    {
        Index = index;
        Tooltips = tooltips;
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

        var markers = ImmutableArray.CreateBuilder<MarkerDefinition>();
        foreach (var type in index.Schema.LinkTypes)
        {
            foreach (var kind in new[] { type.SourceMarker, type.TargetMarker })
            {
                if ((kind == DiagramMarker.None) || markerIds.ContainsKey((type.Name, kind)))
                {
                    continue;
                }

                var id = $"mussel-diagram-{viewId}-m{markers.Count}";
                markerIds[(type.Name, kind)] = id;
                markers.Add(new MarkerDefinition(id, kind, index.LookOf(type).Stroke));
            }
        }

        DefaultMarkerId = $"mussel-diagram-{viewId}-m";
        markers.Add(new MarkerDefinition(DefaultMarkerId, DiagramMarker.Arrow, null));
        Markers = markers.ToImmutable();
    }

    public SchemaIndex Index { get; }

    public bool Tooltips { get; }

    public ImmutableArray<MarkerDefinition> Markers { get; }

    public string DefaultMarkerId { get; }

    public string? FindIconId(string reference) => icons.GetValueOrDefault(reference)?.Id;

    public IconEntry? FindIcon(string reference) => icons.GetValueOrDefault(reference);

    public IconEntry? FindIconById(string id) => iconsById.GetValueOrDefault(id);

    public string? FindMarkerId(string linkType, DiagramMarker kind) => markerIds.GetValueOrDefault((linkType, kind));
}
