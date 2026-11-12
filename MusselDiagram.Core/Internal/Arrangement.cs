namespace MusselDiagram.Internal;

internal enum AlignEdge
{
    Left,
    Center,
    Right,
    Top,
    Middle,
    Bottom
}

internal static class Arrangement
{
    public static List<(string Id, DiagramPoint Offset)> Align(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection, AlignEdge edge)
    {
        var targets = Targets(snapshot, selection);
        if (targets.Count < 2)
        {
            return [];
        }

        var left = targets.Min(static x => x.Bounds.X);
        var top = targets.Min(static x => x.Bounds.Y);
        var right = targets.Max(static x => x.Bounds.Right);
        var bottom = targets.Max(static x => x.Bounds.Bottom);
        var result = new List<(string Id, DiagramPoint Offset)>();
        foreach (var box in targets)
        {
            var bounds = box.Bounds;
            var offset = edge switch
            {
                AlignEdge.Left => new DiagramPoint(left - bounds.X, 0),
                AlignEdge.Center => new DiagramPoint(((left + right) / 2) - bounds.Center.X, 0),
                AlignEdge.Right => new DiagramPoint(right - bounds.Right, 0),
                AlignEdge.Top => new DiagramPoint(0, top - bounds.Y),
                AlignEdge.Middle => new DiagramPoint(0, ((top + bottom) / 2) - bounds.Center.Y),
                _ => new DiagramPoint(0, bottom - bounds.Bottom)
            };
            Add(result, snapshot, box, offset, index);
        }

        return result;
    }

    public static List<(string Id, DiagramPoint Offset)> Distribute(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection, bool horizontal)
    {
        var targets = Targets(snapshot, selection);
        if (targets.Count < 3)
        {
            return [];
        }

        var sorted = targets.OrderBy(x => Center(x, horizontal)).ToList();
        var first = Center(sorted[0], horizontal);
        var last = Center(sorted[^1], horizontal);
        var result = new List<(string Id, DiagramPoint Offset)>();
        for (var i = 1; i < sorted.Count - 1; i++)
        {
            var delta = first + ((last - first) * i / (sorted.Count - 1)) - Center(sorted[i], horizontal);
            Add(result, snapshot, sorted[i], horizontal ? new DiagramPoint(delta, 0) : new DiagramPoint(0, delta), index);
        }

        return result;
    }

    public static bool CanAlign(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> selection, int minimum)
    {
        var targets = Targets(snapshot, selection);
        return (targets.Count >= minimum) && targets.Any(x => ElementPermissions.Allows(x, index, DiagramPermissions.Move));
    }

    private static List<DiagramBox> Targets(DiagramSnapshot snapshot, IEnumerable<string> selection)
    {
        var selected = selection.Distinct(StringComparer.Ordinal).Select(snapshot.Find).OfType<DiagramBox>()
            .Where(static x => x is DiagramNode or DiagramGroup { Kind: DiagramGroupKind.Tree }).ToList();
        var ids = selected.Select(static x => x.Id).ToHashSet(StringComparer.Ordinal);
        return [.. selected.Where(x => !snapshot.GetAncestors(x.Id).Any(a => ids.Contains(a.Id)))];
    }

    private static double Center(DiagramBox box, bool horizontal) => horizontal ? box.Bounds.Center.X : box.Bounds.Center.Y;

    private static void Add(List<(string Id, DiagramPoint Offset)> result, DiagramSnapshot snapshot, DiagramBox box, DiagramPoint offset, SchemaIndex index)
    {
        if (StayRules.Frame(snapshot, index, box) is { } frame)
        {
            offset = StayRules.Clamp(frame, box.Bounds, offset);
        }

        if ((offset != default) && ElementPermissions.Allows(box, index, DiagramPermissions.Move))
        {
            result.Add((box.Id, offset));
        }
    }
}
