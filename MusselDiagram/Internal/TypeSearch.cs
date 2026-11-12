namespace MusselDiagram.Internal;

internal static class TypeSearch
{
    public static List<DiagramBoxType> Search(IReadOnlyList<DiagramBoxType> types, string query, SchemaIndex index, Func<string, string>? normalizer)
    {
        var text = Normalize(query.Trim(), normalizer);
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return [.. types];
        }

        var ranked = new List<(DiagramBoxType Type, int Order, int Rank)>();
        for (var i = 0; i < types.Count; i++)
        {
            var type = types[i];
            var label = Normalize(type.DisplayName ?? type.Name, normalizer);
            var haystack = string.Join('\u0001', [label, Normalize(type.Name, normalizer), Normalize(CategoryLabel(type, index) ?? string.Empty, normalizer), .. type.SearchWords.Select(x => Normalize(x, normalizer))]);
            if (words.All(x => haystack.Contains(x, StringComparison.Ordinal)))
            {
                var rank = string.Equals(label, text, StringComparison.Ordinal) ? 0 : label.StartsWith(text, StringComparison.Ordinal) ? 1 : 2;
                ranked.Add((type, i, rank));
            }
        }

        return [.. ranked.OrderBy(static x => x.Rank).ThenBy(static x => x.Order).Select(static x => x.Type)];
    }

    public static List<string> OrderCategories(IEnumerable<DiagramBoxType> types, SchemaIndex index)
    {
        var order = index.Schema.Categories.Select(static x => x.Name).ToList();
        return [.. types.Select(static x => x.Category).OfType<string>().Distinct(StringComparer.Ordinal).OrderBy(x => order.IndexOf(x) is var i and >= 0 ? i : int.MaxValue)];
    }

    public static IEnumerable<DiagramBoxType> InPaletteOrder(IReadOnlyList<DiagramBoxType> types, SchemaIndex index) =>
        types.Where(static x => x.Category is null)
            .Concat(OrderCategories(types, index).SelectMany(category => types.Where(x => string.Equals(x.Category, category, StringComparison.Ordinal))));

    public static string? CategoryLabel(DiagramBoxType type, SchemaIndex index) =>
        type.Category is { } category ? index.Schema.Categories.FirstOrDefault(x => string.Equals(x.Name, category, StringComparison.Ordinal))?.DisplayName ?? category : null;

    public static List<DiagramBoxType> PaletteTypes(DiagramSnapshot snapshot, SchemaIndex index)
    {
        var rule = index.Schema.Rules.InPalette;
        var result = new List<DiagramBoxType>();
        foreach (var type in index.Schema.NodeTypes.Cast<DiagramBoxType>().Concat(index.Schema.GroupTypes.Where(static x => x.Kind == DiagramGroupKind.Tree)))
        {
            if (!type.InPalette)
            {
                continue;
            }

            if ((type.MaxCount is { } max) && (snapshot.Groups.Cast<DiagramBox>().Concat(snapshot.Nodes).Count(x => ReferenceEquals(index.FindBoxType(x), type)) >= max))
            {
                continue;
            }

            if ((rule is not null) && !rule(new DiagramPaletteContext { Snapshot = snapshot, Type = type.Name }))
            {
                continue;
            }

            result.Add(type);
        }

        return result;
    }

    private static string Normalize(string value, Func<string, string>? normalizer) =>
        (normalizer is null ? value : normalizer(value)).ToUpperInvariant();
}
