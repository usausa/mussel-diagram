namespace MusselDiagram;

using System.Collections.Immutable;
using System.Globalization;

using MusselDiagram.Languages.Japanese;

public sealed class MusselDiagramOptions
{
    public IDictionary<string, DiagramTexts> Cultures { get; } = new Dictionary<string, DiagramTexts>(StringComparer.OrdinalIgnoreCase)
    {
        [string.Empty] = DiagramTexts.English,
        ["ja"] = JapaneseTexts.Default
    };

    internal static MusselDiagramOptions Default { get; } = new();

    internal string SearchPlaceholder => Find(static x => x.SearchPlaceholder) ?? string.Empty;

    internal string NoMatches => Find(static x => x.NoMatches) ?? string.Empty;

    internal string? Reason(DiagramReasonKind kind) => Find(static x => x.Reasons, kind);

    internal string Issue(DiagramIssueKind kind, ImmutableArray<string> arguments) =>
        Format(Find(static x => x.Issues, kind) ?? "{0}", arguments) ?? Format(DiagramTexts.English.Issues.GetValueOrDefault(kind) ?? "{0}", arguments) ?? string.Join(", ", arguments);

    internal string Command(string name) => Find(static x => x.Commands, name) ?? name;

    internal DiagramReason Localize(DiagramReason reason) => reason.Message is null ? reason with { Message = Reason(reason.Kind) } : reason;

    internal DiagramIssue Localize(DiagramIssue issue) => issue.Message is null ? issue with { Message = Issue(issue.Kind, issue.Arguments) } : issue;

    private static string? Format(string text, ImmutableArray<string> arguments)
    {
        try
        {
            return string.Format(CultureInfo.InvariantCulture, text, [.. arguments]);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private string? Find<TKey>(Func<DiagramTexts, IReadOnlyDictionary<TKey, string>> pick, TKey key)
        where TKey : notnull
    {
        for (var culture = CultureInfo.CurrentUICulture; ; culture = culture.Parent)
        {
            if (Cultures.TryGetValue(culture.Name, out var texts) && pick(texts).TryGetValue(key, out var value) && (value.Length > 0))
            {
                return value;
            }

            if (culture.Name.Length == 0)
            {
                return pick(DiagramTexts.English).GetValueOrDefault(key);
            }
        }
    }

    private string? Find(Func<DiagramTexts, string?> pick)
    {
        for (var culture = CultureInfo.CurrentUICulture; ; culture = culture.Parent)
        {
            if (Cultures.TryGetValue(culture.Name, out var texts) && (pick(texts) is { Length: > 0 } value))
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
