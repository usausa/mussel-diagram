namespace MusselDiagram.Internal;

using System.Collections.Immutable;
using System.Globalization;

internal sealed record ElementLook(ImmutableArray<string> States, DiagramRenderOverride? Style, DiagramIssueSeverity? Severity, string? IssueText)
{
    public bool Equals(ElementLook? other) =>
        (other is not null) &&
        States.SequenceEqual(other.States, StringComparer.Ordinal) &&
        Equals(Style, other.Style) &&
        (Severity == other.Severity) &&
        string.Equals(IssueText, other.IssueText, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(States.Length, Style, Severity, IssueText);
}

internal sealed record PlacedBadge(string Corner, DiagramBadge Badge, bool Clickable);

internal sealed record ElementBadges(ImmutableArray<PlacedBadge> Items)
{
    public bool Equals(ElementBadges? other) => (other is not null) && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => Items.Length;
}

internal sealed class OverlayIndex
{
    private static readonly string[] CornerNames = ["top-left", "top-right", "bottom-left", "bottom-right"];

    public static OverlayIndex Empty { get; } = new([], [], []);

    private OverlayIndex(Dictionary<string, ElementLook> looks, Dictionary<string, ElementBadges> badges, List<string> issueIds)
    {
        Looks = looks;
        Badges = badges;
        IssueIds = issueIds;
    }

    public Dictionary<string, ElementLook> Looks { get; }

    public Dictionary<string, ElementBadges> Badges { get; }

    public List<string> IssueIds { get; }

    public static OverlayIndex Build(
        IReadOnlyDictionary<string, DiagramOverlay>? overlays,
        IReadOnlyList<DiagramIssue>? issues,
        IReadOnlyList<DiagramBadgeKind>? badgeKinds,
        OverlayIndex previous)
    {
        var problems = new Dictionary<string, List<DiagramIssue>>(StringComparer.Ordinal);
        var issueIds = new List<string>();
        foreach (var issue in issues ?? [])
        {
            foreach (var id in issue.ElementIds.Distinct(StringComparer.Ordinal))
            {
                if (!problems.TryGetValue(id, out var list))
                {
                    list = [];
                    problems[id] = list;
                    issueIds.Add(id);
                }

                list.Add(issue);
            }
        }

        var kinds = new Dictionary<string, DiagramBadgeKind>(StringComparer.Ordinal);
        foreach (var kind in badgeKinds ?? [])
        {
            kinds.TryAdd(kind.Name, kind);
        }

        var looks = new Dictionary<string, ElementLook>(StringComparer.Ordinal);
        var badges = new Dictionary<string, ElementBadges>(StringComparer.Ordinal);
        foreach (var id in (overlays?.Keys ?? []).Concat(issueIds).Distinct(StringComparer.Ordinal))
        {
            var overlay = overlays?.GetValueOrDefault(id);
            var found = problems.GetValueOrDefault(id);
            var severity = found is null ? (DiagramIssueSeverity?)null : found.Any(static x => x.Severity == DiagramIssueSeverity.Error) ? DiagramIssueSeverity.Error : DiagramIssueSeverity.Warning;
            var text = found is null ? null : string.Join('\n', found.Select(static x => x.Message).Where(static x => !string.IsNullOrEmpty(x)));
            var states = overlay is null ? [] : overlay.States.Where(IsStateName).Distinct(StringComparer.Ordinal).ToImmutableArray();
            var look = new ElementLook(states, overlay?.Style, severity, string.IsNullOrEmpty(text) ? null : text);
            if (!look.States.IsEmpty || (look.Style is not null) || (look.Severity is not null))
            {
                looks[id] = Reuse(previous.Looks, id, look);
            }

            var placed = Place(overlay, found, severity, text, kinds);
            if (!placed.IsEmpty)
            {
                badges[id] = Reuse(previous.Badges, id, new ElementBadges(placed));
            }
        }

        return new OverlayIndex(looks, badges, issueIds);
    }

    private static ImmutableArray<PlacedBadge> Place(DiagramOverlay? overlay, List<DiagramIssue>? found, DiagramIssueSeverity? severity, string? text, Dictionary<string, DiagramBadgeKind> kinds)
    {
        var items = new List<(int Corner, int Rank, int Order, int Index, PlacedBadge Badge)>();
        if (found is not null)
        {
            var badge = new DiagramBadge
            {
                Text = found.Count > 1 ? found.Count.ToString(CultureInfo.InvariantCulture) : null,
                Tone = severity == DiagramIssueSeverity.Error ? DiagramBadgeTone.Error : DiagramBadgeTone.Warning,
                Tooltip = text
            };
            items.Add(((int)DiagramCorner.TopRight, 0, 0, 0, new PlacedBadge(CornerNames[(int)DiagramCorner.TopRight], badge, false)));
        }

        var badges = overlay?.Badges ?? [];
        for (var i = 0; i < badges.Length; i++)
        {
            var badge = badges[i];
            var kind = badge.Kind is null ? null : kinds.GetValueOrDefault(badge.Kind);
            var corner = (int)(kind?.Corner ?? DiagramCorner.TopRight);
            items.Add((corner, kind is null ? 2 : 1, kind?.Order ?? 0, i, new PlacedBadge(CornerNames[corner], badge, kind?.Clickable ?? false)));
        }

        return [.. items.OrderBy(static x => x.Corner).ThenBy(static x => x.Rank).ThenBy(static x => x.Order).ThenBy(static x => x.Index).Select(static x => x.Badge)];
    }

    private static T Reuse<T>(Dictionary<string, T> previous, string id, T value)
        where T : class =>
        previous.TryGetValue(id, out var old) && old.Equals(value) ? old : value;

    private static bool IsStateName(string name) =>
        (name.Length > 0) && name.All(static c => char.IsAsciiLetterOrDigit(c) || (c == '-'));
}
