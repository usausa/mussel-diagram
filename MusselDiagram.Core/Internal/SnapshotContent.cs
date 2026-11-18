namespace MusselDiagram.Internal;

using System.Collections.Immutable;
using System.Text.Json;

internal static class SnapshotContent
{
    public static bool Same(DiagramSnapshot x, DiagramSnapshot y) =>
        ReferenceEquals(x, y) ||
        (Same(x.Groups, y.Groups) && Same(x.Nodes, y.Nodes) && Same(x.Links, y.Links) && Same(x.SharedItems, y.SharedItems));

    public static bool Same(DiagramElement x, DiagramElement y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if ((x.GetType() != y.GetType()) ||
            !String.Equals(x.Id, y.Id, StringComparison.Ordinal) ||
            !String.Equals(x.Type, y.Type, StringComparison.Ordinal) ||
            !SameData(x.Data, y.Data))
        {
            return false;
        }

        return (x, y) switch
        {
            (DiagramGroup a, DiagramGroup b) => SameBox(a, b) && (a.Kind == b.Kind) && a.MemberIds.SequenceEqual(b.MemberIds, StringComparer.Ordinal),
            (DiagramBox a, DiagramBox b) => SameBox(a, b),
            (DiagramLink a, DiagramLink b) =>
                (a.Source == b.Source) && (a.Target == b.Target) && a.Route.SequenceEqual(b.Route) && (a.RouteState == b.RouteState) &&
                String.Equals(a.RouteError, b.RouteError, StringComparison.Ordinal) && (a.Look == b.Look) && (a.Locked == b.Locked) && (a.Permissions == b.Permissions),
            (DiagramSharedItem a, DiagramSharedItem b) => String.Equals(a.Label, b.Label, StringComparison.Ordinal),
            _ => false
        };
    }

    public static bool SameData(object? x, object? y) =>
        ReferenceEquals(x, y) || ((x is JsonElement a) && (y is JsonElement b) ? JsonElement.DeepEquals(a, b) : Equals(x, y));

    public static bool SameTexts(ImmutableDictionary<string, string> x, ImmutableDictionary<string, string> y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x.Count != y.Count)
        {
            return false;
        }

        foreach (var (key, value) in x)
        {
            if (!y.TryGetValue(key, out var other) || !String.Equals(value, other, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Same<T>(ImmutableArray<T> x, ImmutableArray<T> y)
        where T : DiagramElement
    {
        if (x == y)
        {
            return true;
        }

        if (x.Length != y.Length)
        {
            return false;
        }

        for (var i = 0; i < x.Length; i++)
        {
            if (!Same(x[i], y[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameBox(DiagramBox x, DiagramBox y) =>
        (x.Bounds == y.Bounds) &&
        String.Equals(x.ParentId, y.ParentId, StringComparison.Ordinal) &&
        String.Equals(x.Label, y.Label, StringComparison.Ordinal) &&
        SameTexts(x.Texts, y.Texts) &&
        String.Equals(x.Icon, y.Icon, StringComparison.Ordinal) &&
        (x.Look == y.Look) &&
        (x.Locked == y.Locked) &&
        (x.Permissions == y.Permissions);
}
