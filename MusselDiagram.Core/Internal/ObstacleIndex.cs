namespace MusselDiagram.Internal;

internal sealed class ObstacleIndex
{
    private const double CellSize = 160;

    private const int MaxCells = 4096;

    private readonly Dictionary<(int X, int Y), List<int>> cells = [];

    private readonly List<int> large = [];

    private readonly DiagramRect[] rects;

    private readonly string[] ids;

    public ObstacleIndex(IReadOnlyList<DiagramNode> nodes)
    {
        rects = new DiagramRect[nodes.Count];
        ids = new string[nodes.Count];
        var left = double.PositiveInfinity;
        var top = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        var bottom = double.NegativeInfinity;
        for (var i = 0; i < nodes.Count; i++)
        {
            var bounds = nodes[i].Bounds;
            rects[i] = bounds;
            ids[i] = nodes[i].Id;
            left = Math.Min(left, bounds.X);
            top = Math.Min(top, bounds.Y);
            right = Math.Max(right, bounds.Right);
            bottom = Math.Max(bottom, bounds.Bottom);
            var (x0, y0, x1, y1) = Range(bounds);
            if (((long)(x1 - x0 + 1) * (y1 - y0 + 1)) > MaxCells)
            {
                large.Add(i);
                continue;
            }

            for (var x = x0; x <= x1; x++)
            {
                for (var y = y0; y <= y1; y++)
                {
                    if (!cells.TryGetValue((x, y), out var list))
                    {
                        list = [];
                        cells[(x, y)] = list;
                    }

                    list.Add(i);
                }
            }
        }

        Bounds = nodes.Count == 0 ? default : new DiagramRect(left, top, right - left, bottom - top);
    }

    public DiagramRect Bounds { get; }

    public void All(string exceptA, string exceptB, List<DiagramRect> output)
    {
        for (var i = 0; i < rects.Length; i++)
        {
            if (!string.Equals(ids[i], exceptA, StringComparison.Ordinal) && !string.Equals(ids[i], exceptB, StringComparison.Ordinal))
            {
                output.Add(rects[i]);
            }
        }
    }

    public void Query(DiagramRect area, string exceptA, string exceptB, List<DiagramRect> output)
    {
        var (qx0, qy0, qx1, qy1) = Range(area);
        if (((long)(qx1 - qx0 + 1) * (qy1 - qy0 + 1)) > MaxCells)
        {
            for (var i = 0; i < rects.Length; i++)
            {
                Take(i);
            }

            return;
        }

        foreach (var i in large)
        {
            Take(i);
        }

        for (var x = qx0; x <= qx1; x++)
        {
            for (var y = qy0; y <= qy1; y++)
            {
                if (!cells.TryGetValue((x, y), out var list))
                {
                    continue;
                }

                foreach (var i in list)
                {
                    var (rx0, ry0, _, _) = Range(rects[i]);
                    if ((x == Math.Max(rx0, qx0)) && (y == Math.Max(ry0, qy0)))
                    {
                        Take(i);
                    }
                }
            }
        }

        void Take(int i)
        {
            var rect = rects[i];
            if ((rect.Right < area.X) || (rect.X > area.Right) || (rect.Bottom < area.Y) || (rect.Y > area.Bottom) ||
                string.Equals(ids[i], exceptA, StringComparison.Ordinal) || string.Equals(ids[i], exceptB, StringComparison.Ordinal))
            {
                return;
            }

            output.Add(rect);
        }
    }

    private static int Cell(double value) => (int)Math.Floor(Math.Clamp(value / CellSize, -1e9, 1e9));

    private static (int X0, int Y0, int X1, int Y1) Range(DiagramRect rect) =>
        (Cell(rect.X), Cell(rect.Y), Cell(rect.Right), Cell(rect.Bottom));
}
