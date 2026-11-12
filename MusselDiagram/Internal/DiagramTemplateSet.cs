namespace MusselDiagram.Internal;

internal sealed class DiagramTemplateSet
{
    private readonly Dictionary<string, DiagramTemplate> byType = new(StringComparer.Ordinal);

    private readonly Dictionary<DiagramTemplate, string> types = [];

    public int Version { get; private set; }

    public bool IsEmpty => byType.Count == 0;

    public DiagramTemplate? Find(string type) => byType.GetValueOrDefault(type);

    public bool Set(DiagramTemplate template)
    {
        if (types.TryGetValue(template, out var type))
        {
            if (string.Equals(type, template.Type, StringComparison.Ordinal))
            {
                return false;
            }

            Remove(template);
        }

        types[template] = template.Type;
        byType[template.Type] = template;
        Version++;
        return true;
    }

    public bool Remove(DiagramTemplate template)
    {
        if (!types.Remove(template, out var type))
        {
            return false;
        }

        if (ReferenceEquals(byType.GetValueOrDefault(type), template))
        {
            byType.Remove(type);
            if (types.FirstOrDefault(x => string.Equals(x.Value, type, StringComparison.Ordinal)).Key is { } other)
            {
                byType[type] = other;
            }
        }

        Version++;
        return true;
    }
}
