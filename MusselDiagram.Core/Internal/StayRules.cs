namespace MusselDiagram.Internal;

internal static class StayRules
{
    private const double Tolerance = 0.01;

    public static DiagramRect? Frame(DiagramSnapshot snapshot, SchemaIndex index, DiagramBox box)
    {
        if ((box.ParentId is not { } parentId) || (index.FindBoxType(box) is not { StayInParent: true }) ||
            (snapshot.Find(parentId) is not DiagramGroup { Kind: DiagramGroupKind.Tree } parent))
        {
            return null;
        }

        var type = index.FindBoxType(parent) as DiagramGroupType;
        if (type is { Sizing: DiagramGroupSizing.Content })
        {
            return null;
        }

        var padding = type?.Padding ?? default;
        var bounds = parent.Bounds;
        return new DiagramRect(bounds.X + padding.Left, bounds.Y + padding.Top, bounds.Width - padding.Left - padding.Right, bounds.Height - padding.Top - padding.Bottom);
    }

    public static DiagramPoint Clamp(DiagramRect frame, DiagramRect bounds, DiagramPoint offset)
    {
        var minX = frame.X - bounds.X;
        var maxX = frame.Right - bounds.Right;
        var minY = frame.Y - bounds.Y;
        var maxY = frame.Bottom - bounds.Bottom;
        return new DiagramPoint(Math.Min(Math.Max(offset.X, minX), Math.Max(minX, maxX)), Math.Min(Math.Max(offset.Y, minY), Math.Max(minY, maxY)));
    }

    public static bool Allows(DiagramSnapshot snapshot, SchemaIndex index, IEnumerable<string> ids, DiagramPoint offset)
    {
        var moving = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            switch (snapshot.Find(id))
            {
                case DiagramGroup { Kind: DiagramGroupKind.Overlay } overlay:
                    moving.UnionWith(overlay.MemberIds);
                    break;
                case DiagramBox box:
                    moving.Add(box.Id);
                    break;
            }
        }

        foreach (var id in moving)
        {
            if ((snapshot.Find(id) is not DiagramBox box) || ((box.ParentId is { } parentId) && moving.Contains(parentId)) ||
                (Frame(snapshot, index, box) is not { } frame))
            {
                continue;
            }

            var clamped = Clamp(frame, box.Bounds, offset);
            if ((Math.Abs(clamped.X - offset.X) > Tolerance) || (Math.Abs(clamped.Y - offset.Y) > Tolerance))
            {
                return false;
            }
        }

        return true;
    }
}
