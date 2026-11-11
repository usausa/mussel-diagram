namespace MusselDiagram.Internal;

using System.Collections.Immutable;

internal static class OrthogonalRouter
{
    public const double BendPenalty = 30;

    private const int MaxNodes = 250_000;

    private static readonly double[] RegionMargins = [40, 160, 640];

    [ThreadStatic]
    private static Scratch? scratch;

    public static ImmutableArray<DiagramPoint> Route(LinkAnchor start, LinkAnchor end, double stub, double margin, ObstacleIndex obstacles, string sourceId, string targetId, DiagramRect source, DiagramRect target)
    {
        var simple = LinkRouting.Orthogonal(start, end, stub);
        var found = new List<DiagramRect>();
        if (IsClear(simple, obstacles, sourceId, targetId, margin, source, target, found))
        {
            return simple;
        }

        var endMargin = Math.Min(margin, stub / 2);
        var startOut = LinkRouting.Step(start.Position, start.Side, stub);
        var endOut = LinkRouting.Step(end.Position, end.Side, stub);
        if ((startOut != endOut) && (Trapped(startOut) || Trapped(endOut)))
        {
            return simple;
        }

        foreach (var regionMargin in RegionMargins)
        {
            var region = Region(startOut, endOut, regionMargin);
            if (Search(start, end, startOut, endOut, Blockers(obstacles, Inflate(region, margin), sourceId, targetId, margin, endMargin, source, target), region) is { } route)
            {
                return route;
            }
        }

        var blockers = new List<DiagramRect>();
        obstacles.All(sourceId, targetId, blockers);
        var all = Region(startOut, endOut, margin);
        for (var i = 0; i < blockers.Count; i++)
        {
            blockers[i] = Inflate(blockers[i], margin);
            all = Union(all, blockers[i]);
        }

        blockers.Add(Inflate(source, endMargin));
        blockers.Add(Inflate(target, endMargin));
        all = Union(Union(all, blockers[^1]), blockers[^2]);
        return Search(start, end, startOut, endOut, blockers, Inflate(all, 40)) ?? simple;

        bool Trapped(DiagramPoint point)
        {
            if (Inside(point, Inflate(source, endMargin)) || Inside(point, Inflate(target, endMargin)))
            {
                return true;
            }

            found.Clear();
            obstacles.Query(new DiagramRect(point.X - margin, point.Y - margin, margin * 2, margin * 2), sourceId, targetId, found);
            foreach (var obstacle in found)
            {
                if (Inside(point, Inflate(obstacle, margin)))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static ImmutableArray<DiagramPoint>? Clear(LinkAnchor start, LinkAnchor end, double stub, double margin, ObstacleIndex obstacles, string sourceId, string targetId, DiagramRect source, DiagramRect target)
    {
        var simple = LinkRouting.Orthogonal(start, end, stub);
        return IsClear(simple, obstacles, sourceId, targetId, margin, source, target, []) ? simple : null;
    }

    public static bool Avoids(ImmutableArray<DiagramPoint> route, ObstacleIndex obstacles, string sourceId, string targetId, double margin, DiagramRect source, DiagramRect target) =>
        IsClear(route, obstacles, sourceId, targetId, margin, source, target, []);

    public static bool Crosses(ImmutableArray<DiagramPoint> route, DiagramRect rect)
    {
        for (var i = 1; i < route.Length; i++)
        {
            if (SegmentCrosses(route[i - 1], route[i], rect))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsClear(ImmutableArray<DiagramPoint> route, ObstacleIndex obstacles, string sourceId, string targetId, double margin, DiagramRect source, DiagramRect target, List<DiagramRect> found)
    {
        if (Crosses(route, source) || Crosses(route, target))
        {
            return false;
        }

        for (var i = 1; i < route.Length; i++)
        {
            var a = route[i - 1];
            var b = route[i];
            var area = new DiagramRect(Math.Min(a.X, b.X) - margin, Math.Min(a.Y, b.Y) - margin, Math.Abs(a.X - b.X) + (margin * 2), Math.Abs(a.Y - b.Y) + (margin * 2));
            found.Clear();
            obstacles.Query(area, sourceId, targetId, found);
            foreach (var obstacle in found)
            {
                if (SegmentCrosses(a, b, Inflate(obstacle, margin)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static List<DiagramRect> Blockers(ObstacleIndex obstacles, DiagramRect area, string sourceId, string targetId, double margin, double endMargin, DiagramRect source, DiagramRect target)
    {
        var blockers = new List<DiagramRect>();
        obstacles.Query(area, sourceId, targetId, blockers);
        for (var i = 0; i < blockers.Count; i++)
        {
            blockers[i] = Inflate(blockers[i], margin);
        }

        blockers.Add(Inflate(source, endMargin));
        blockers.Add(Inflate(target, endMargin));
        return blockers;
    }

    private static bool SegmentCrosses(DiagramPoint a, DiagramPoint b, DiagramRect rect)
    {
        var left = Math.Min(a.X, b.X);
        var right = Math.Max(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var bottom = Math.Max(a.Y, b.Y);
        if ((right <= rect.X) || (left >= rect.Right) || (bottom <= rect.Y) || (top >= rect.Bottom))
        {
            return false;
        }

        if (a.X.Equals(b.X) || a.Y.Equals(b.Y))
        {
            return true;
        }

        return Intersects(a, b, new DiagramPoint(rect.X, rect.Y), new DiagramPoint(rect.Right, rect.Bottom)) ||
            Intersects(a, b, new DiagramPoint(rect.Right, rect.Y), new DiagramPoint(rect.X, rect.Bottom)) ||
            Inside(a, rect) || Inside(b, rect);
    }

    private static bool Inside(DiagramPoint point, DiagramRect rect) =>
        (point.X > rect.X) && (point.X < rect.Right) && (point.Y > rect.Y) && (point.Y < rect.Bottom);

    private static bool Intersects(DiagramPoint a, DiagramPoint b, DiagramPoint c, DiagramPoint d)
    {
        var d1 = Cross(c, d, a);
        var d2 = Cross(c, d, b);
        var d3 = Cross(a, b, c);
        var d4 = Cross(a, b, d);
        return (((d1 > 0) && (d2 < 0)) || ((d1 < 0) && (d2 > 0))) && (((d3 > 0) && (d4 < 0)) || ((d3 < 0) && (d4 > 0)));
    }

    private static double Cross(DiagramPoint origin, DiagramPoint a, DiagramPoint b) =>
        ((a.X - origin.X) * (b.Y - origin.Y)) - ((a.Y - origin.Y) * (b.X - origin.X));

    private static DiagramRect Inflate(DiagramRect rect, double amount) =>
        new(rect.X - amount, rect.Y - amount, rect.Width + (amount * 2), rect.Height + (amount * 2));

    private static DiagramRect Union(DiagramRect a, DiagramRect b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        return new DiagramRect(left, top, Math.Max(a.Right, b.Right) - left, Math.Max(a.Bottom, b.Bottom) - top);
    }

    private static DiagramRect Region(DiagramPoint a, DiagramPoint b, double margin)
    {
        var left = Math.Min(a.X, b.X) - margin;
        var top = Math.Min(a.Y, b.Y) - margin;
        return new DiagramRect(left, top, Math.Abs(a.X - b.X) + (margin * 2), Math.Abs(a.Y - b.Y) + (margin * 2));
    }

    private static double[] Coordinates(List<double> values, List<DiagramRect> blockers, bool horizontal, double first, double second, double low, double high)
    {
        values.Clear();
        values.Add(first);
        values.Add(second);
        values.Add(low);
        values.Add(high);
        foreach (var blocker in blockers)
        {
            var a = horizontal ? blocker.X : blocker.Y;
            var b = horizontal ? blocker.Right : blocker.Bottom;
            if ((a > low) && (a < high))
            {
                values.Add(a);
            }

            if ((b > low) && (b < high))
            {
                values.Add(b);
            }
        }

        values.Sort();
        var count = 0;
        for (var i = 0; i < values.Count; i++)
        {
            if ((count == 0) || !values[i].Equals(values[count - 1]))
            {
                values[count++] = values[i];
            }
        }

        var result = new double[count];
        values.CopyTo(0, result, 0, count);
        return result;
    }

    private static int FirstAtLeast(double[] values, double value)
    {
        var index = Array.BinarySearch(values, value);
        return index >= 0 ? index : ~index;
    }

    private static int FirstAbove(double[] values, double value)
    {
        var index = Array.BinarySearch(values, value);
        return index >= 0 ? index + 1 : ~index;
    }

    private static int LastAtMost(double[] values, double value)
    {
        var index = Array.BinarySearch(values, value);
        return index >= 0 ? index : ~index - 1;
    }

    private static int LastBelow(double[] values, double value)
    {
        var index = Array.BinarySearch(values, value);
        return index >= 0 ? index - 1 : ~index - 1;
    }

    private static ImmutableArray<DiagramPoint>? Search(LinkAnchor start, LinkAnchor end, DiagramPoint startOut, DiagramPoint endOut, List<DiagramRect> blockers, DiagramRect region)
    {
        var work = scratch ??= new Scratch();
        var inside = work.InRegion;
        inside.Clear();
        foreach (var blocker in blockers)
        {
            if ((blocker.Right > region.X) && (blocker.X < region.Right) && (blocker.Bottom > region.Y) && (blocker.Y < region.Bottom))
            {
                inside.Add(blocker);
            }
        }

        var xs = Coordinates(work.Values, inside, true, startOut.X, endOut.X, region.X, region.Right);
        var ys = Coordinates(work.Values, inside, false, startOut.Y, endOut.Y, region.Y, region.Bottom);
        var width = xs.Length;
        var height = ys.Length;
        if ((width * height) > MaxNodes)
        {
            return null;
        }

        var generation = work.Prepare(width * height);
        var blockedNode = work.BlockedNode;
        var blockedRight = work.BlockedRight;
        var blockedDown = work.BlockedDown;
        foreach (var blocker in inside)
        {
            var columnFrom = FirstAtLeast(xs, blocker.X);
            var columnTo = LastAtMost(xs, blocker.Right);
            var innerColumnFrom = FirstAbove(xs, blocker.X);
            var innerColumnTo = LastBelow(xs, blocker.Right);
            var rowFrom = FirstAtLeast(ys, blocker.Y);
            var rowTo = LastAtMost(ys, blocker.Bottom);
            var innerRowFrom = FirstAbove(ys, blocker.Y);
            var innerRowTo = LastBelow(ys, blocker.Bottom);
            for (var row = innerRowFrom; row <= innerRowTo; row++)
            {
                for (var column = columnFrom; column < columnTo; column++)
                {
                    blockedRight[(row * width) + column] = generation;
                }

                for (var column = innerColumnFrom; column <= innerColumnTo; column++)
                {
                    blockedNode[(row * width) + column] = generation;
                }
            }

            for (var column = innerColumnFrom; column <= innerColumnTo; column++)
            {
                for (var row = rowFrom; row < rowTo; row++)
                {
                    blockedDown[(row * width) + column] = generation;
                }
            }
        }

        var startNode = (Array.BinarySearch(ys, startOut.Y) * width) + Array.BinarySearch(xs, startOut.X);
        var endNode = (Array.BinarySearch(ys, endOut.Y) * width) + Array.BinarySearch(xs, endOut.X);
        blockedNode[startNode] = 0;
        blockedNode[endNode] = 0;

        var startAxis = LinkRouting.IsHorizontal(start.Side) ? 0 : 1;
        var endAxis = LinkRouting.IsHorizontal(end.Side) ? 0 : 1;
        var goal = width * height * 2;
        var stamp = work.Stamp;
        var cost = work.Cost;
        var parent = work.Parent;
        var closed = work.Closed;
        var queue = work.Queue;
        queue.Clear();
        var order = 0L;
        var first = (startNode * 2) + startAxis;
        Set(first, 0, -1);
        queue.Enqueue(first, (Estimate(startNode, startAxis), Estimate(startNode, startAxis), order++));
        while (queue.TryDequeue(out var state, out _))
        {
            if (state == goal)
            {
                return Build(start, end, startOut, endOut, parent, goal, xs, ys, width);
            }

            if (closed[state] == generation)
            {
                continue;
            }

            closed[state] = generation;
            var node = state / 2;
            var axis = state % 2;
            var current = cost[state];
            if (node == endNode)
            {
                var total = current + (axis == endAxis ? 0 : BendPenalty);
                if (total < CostOf(goal))
                {
                    Set(goal, total, state);
                    queue.Enqueue(goal, (total, 0, order++));
                }

                continue;
            }

            var column = node % width;
            var row = node / width;
            for (var next = 0; next < 4; next++)
            {
                var target = Neighbor(column, row, next);
                if (target < 0)
                {
                    continue;
                }

                var nextAxis = next % 2;
                var nextState = (target * 2) + nextAxis;
                var length = Math.Abs(xs[target % width] - xs[column]) + Math.Abs(ys[target / width] - ys[row]);
                var nextCost = current + length + (nextAxis == axis ? 0 : BendPenalty);
                if (nextCost < CostOf(nextState))
                {
                    Set(nextState, nextCost, state);
                    var estimate = Estimate(target, nextAxis);
                    queue.Enqueue(nextState, (nextCost + estimate, estimate, order++));
                }
            }
        }

        return null;

        double Estimate(int node, int axis)
        {
            var dx = Math.Abs(xs[node % width] - endOut.X);
            var dy = Math.Abs(ys[node / width] - endOut.Y);
            int bends;
            if ((dx == 0) && (dy == 0))
            {
                bends = axis == endAxis ? 0 : 1;
            }
            else if (dy == 0)
            {
                bends = (axis == 0 ? 0 : 1) + (endAxis == 0 ? 0 : 1);
            }
            else if (dx == 0)
            {
                bends = (axis == 1 ? 0 : 1) + (endAxis == 1 ? 0 : 1);
            }
            else
            {
                bends = axis == endAxis ? 2 : 1;
            }

            return dx + dy + (bends * BendPenalty);
        }

        double CostOf(int state) => stamp[state] == generation ? cost[state] : double.PositiveInfinity;

        void Set(int state, double value, int from)
        {
            stamp[state] = generation;
            cost[state] = value;
            parent[state] = from;
        }

        int Neighbor(int column, int row, int direction)
        {
            int target;
            switch (direction)
            {
                case 0:
                    if ((column + 1 >= width) || (blockedRight[(row * width) + column] == generation))
                    {
                        return -1;
                    }

                    target = (row * width) + column + 1;
                    break;
                case 1:
                    if ((row + 1 >= height) || (blockedDown[(row * width) + column] == generation))
                    {
                        return -1;
                    }

                    target = ((row + 1) * width) + column;
                    break;
                case 2:
                    if ((column == 0) || (blockedRight[(row * width) + column - 1] == generation))
                    {
                        return -1;
                    }

                    target = (row * width) + column - 1;
                    break;
                default:
                    if ((row == 0) || (blockedDown[((row - 1) * width) + column] == generation))
                    {
                        return -1;
                    }

                    target = ((row - 1) * width) + column;
                    break;
            }

            return (blockedNode[target] == generation) && (target != endNode) ? -1 : target;
        }
    }

    private static ImmutableArray<DiagramPoint> Build(LinkAnchor start, LinkAnchor end, DiagramPoint startOut, DiagramPoint endOut, int[] parent, int goal, double[] xs, double[] ys, int width)
    {
        var nodes = new List<DiagramPoint>();
        var state = parent[goal];
        while (state >= 0)
        {
            var node = state / 2;
            nodes.Add(new DiagramPoint(xs[node % width], ys[node / width]));
            state = parent[state];
        }

        nodes.Reverse();
        var points = new List<DiagramPoint>(nodes.Count + 4) { start.Position, startOut };
        points.AddRange(nodes);
        points.Add(endOut);
        points.Add(end.Position);
        return LinkRouting.Simplify(points);
    }

    private sealed class Scratch
    {
        private int generation;

        public List<DiagramRect> InRegion { get; } = [];

        public List<double> Values { get; } = [];

        public PriorityQueue<int, (double Total, double Remaining, long Order)> Queue { get; } = new();

        public int[] BlockedNode { get; private set; } = [];

        public int[] BlockedRight { get; private set; } = [];

        public int[] BlockedDown { get; private set; } = [];

        public int[] Stamp { get; private set; } = [];

        public double[] Cost { get; private set; } = [];

        public int[] Parent { get; private set; } = [];

        public int[] Closed { get; private set; } = [];

        public int Prepare(int nodes)
        {
            var states = (nodes * 2) + 1;
            if (BlockedNode.Length < nodes)
            {
                BlockedNode = new int[nodes];
                BlockedRight = new int[nodes];
                BlockedDown = new int[nodes];
            }

            if (Stamp.Length < states)
            {
                Stamp = new int[states];
                Cost = new double[states];
                Parent = new int[states];
                Closed = new int[states];
            }

            if (generation == int.MaxValue)
            {
                generation = 0;
                Array.Clear(BlockedNode);
                Array.Clear(BlockedRight);
                Array.Clear(BlockedDown);
                Array.Clear(Stamp);
                Array.Clear(Closed);
            }

            return ++generation;
        }
    }
}
