namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal sealed record MarkerDefinition(string Id, DiagramMarker Kind, string? Color);

internal sealed record IconDefinition(string Id, DiagramIcon Icon, string Body);

internal sealed class RenderContext
{
    private readonly Dictionary<string, string> iconIds = new(StringComparer.Ordinal);

    private readonly Dictionary<(string Type, DiagramMarker Kind), string> markerIds = [];

    public RenderContext(SchemaIndex index, string viewId, bool tooltips)
    {
        Index = index;
        Tooltips = tooltips;
        var icons = ImmutableArray.CreateBuilder<IconDefinition>();
        foreach (var set in index.Schema.IconSets)
        {
            foreach (var icon in set.Icons)
            {
                var reference = $"{set.Prefix}:{icon.Id}";
                if (iconIds.ContainsKey(reference))
                {
                    continue;
                }

                var id = $"mussel-diagram-{viewId}-i{icons.Count}";
                iconIds[reference] = id;
                icons.Add(new IconDefinition(id, icon, SvgSanitizer.Body(set, icon)));
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
        Icons = icons.ToImmutable();
        Markers = markers.ToImmutable();
    }

    public SchemaIndex Index { get; }

    public bool Tooltips { get; }

    public ImmutableArray<IconDefinition> Icons { get; }

    public ImmutableArray<MarkerDefinition> Markers { get; }

    public string DefaultMarkerId { get; }

    public string? FindIconId(string reference) => iconIds.GetValueOrDefault(reference);

    public string? FindMarkerId(string linkType, DiagramMarker kind) => markerIds.GetValueOrDefault((linkType, kind));
}
