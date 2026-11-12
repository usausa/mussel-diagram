namespace MusselDiagram;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;

using MusselDiagram.Languages.Japanese;

public static class DiagramCultures
{
    public static IDictionary<string, DiagramTexts> Texts { get; } = new ConcurrentDictionary<string, DiagramTexts>(StringComparer.OrdinalIgnoreCase)
    {
        [string.Empty] = DiagramTexts.English,
        ["ja"] = JapaneseLanguage.Texts
    };

    internal static string SearchPlaceholder => Find(static x => x.SearchPlaceholder) ?? string.Empty;

    internal static string NoMatches => Find(static x => x.NoMatches) ?? string.Empty;

    internal static string? Reason(DiagramReasonKind kind) => Find(static x => x.Reasons, kind);

    internal static string Issue(DiagramIssueKind kind, ImmutableArray<string> arguments) =>
        string.Format(CultureInfo.InvariantCulture, Find(static x => x.Issues, kind) ?? "{0}", [.. arguments]);

    internal static string Command(string name) => Find(static x => x.Commands, name) ?? name;

    private static string? Find<TKey>(Func<DiagramTexts, IReadOnlyDictionary<TKey, string>> pick, TKey key)
        where TKey : notnull
    {
        for (var culture = CultureInfo.CurrentUICulture; ; culture = culture.Parent)
        {
            if (Texts.TryGetValue(culture.Name, out var texts) && pick(texts).TryGetValue(key, out var value) && (value.Length > 0))
            {
                return value;
            }

            if (culture.Name.Length == 0)
            {
                return pick(DiagramTexts.English).GetValueOrDefault(key);
            }
        }
    }

    private static string? Find(Func<DiagramTexts, string?> pick)
    {
        for (var culture = CultureInfo.CurrentUICulture; ; culture = culture.Parent)
        {
            if (Texts.TryGetValue(culture.Name, out var texts) && (pick(texts) is { Length: > 0 } value))
            {
                return value;
            }

            if (culture.Name.Length == 0)
            {
                return pick(DiagramTexts.English);
            }
        }
    }
}
