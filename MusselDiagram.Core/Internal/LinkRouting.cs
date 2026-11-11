namespace MusselDiagram.Internal;

using System.Collections.Immutable;
using System.Globalization;
using System.Text;

internal readonly record struct LinkAnchor(DiagramPoint Position, DiagramSide Side);

internal sealed record PortPlacement(DiagramPortDefinition Definition, double Ratio)
{
    public string Id => Definition.Id;

    public DiagramSide Side => Definition.Side;

    public double Offset => Definition.Offset;

    public DiagramPoint PositionIn(DiagramRect bounds) => LinkRouting.PortPosition(bounds, Side, Ratio, Offset);
}

internal static class LinkRouting
{
    public const double DefaultStubLength = 20;

    public static DiagramPoint PortPosition(DiagramRect bounds, DiagramSide side, double ratio, double offset) => side switch
    {
        DiagramSide.Top => new(bounds.X + (bounds.Width * ratio), bounds.Y - offset),
        DiagramSide.Right => new(bounds.Right + offset, bounds.Y + (bounds.Height * ratio)),
        DiagramSide.Bottom => new(bounds.X + (bounds.Width * ratio), bounds.Bottom + offset),
        _ => new(bounds.X - offset, bounds.Y + (bounds.Height * ratio))
    };

    public static ImmutableArray<PortPlacement> Place(ImmutableArray<DiagramPortDefinition> ports)
    {
        var builder = ImmutableArray.CreateBuilder<PortPlacement>(ports.Length);
        for (var i = 0; i < ports.Length; i++)
        {
            var port = ports[i];
            var ratio = port.Ratio;
            if (ratio is null)
            {
                var before = 0;
                var count = 0;
                for (var j = 0; j < ports.Length; j++)
                {
                    if ((ports[j].Side == port.Side) && (ports[j].Ratio is null))
                    {
                        count++;
                        if (j < i)
                        {
                            before++;
                        }
                    }
                }

                ratio = (before + 1) / (double)(count + 1);
            }

            builder.Add(new PortPlacement(port, ratio.Value));
        }

        return builder.MoveToImmutable();
    }

    public static ImmutableArray<DiagramPort> GetPorts(DiagramBox box, ImmutableArray<PortPlacement> ports)
    {
        var builder = ImmutableArray.CreateBuilder<DiagramPort>(ports.Length);
        foreach (var port in ports)
        {
            var definition = port.Definition;
            builder.Add(new DiagramPort
            {
                Id = definition.Id,
                Direction = definition.Direction,
                Kind = definition.Kind,
                Side = definition.Side,
                Position = port.PositionIn(box.Bounds),
                Label = definition.Label,
                Visibility = definition.Visibility
            });
        }

        return builder.MoveToImmutable();
    }

    public static ImmutableArray<DiagramPoint> Route(DiagramBox source, PortPlacement? sourcePort, DiagramBox target, PortPlacement? targetPort, DiagramLinkType? linkType)
    {
        var sourceToward = targetPort?.PositionIn(target.Bounds) ?? target.Bounds.Center;
        var targetToward = sourcePort?.PositionIn(source.Bounds) ?? source.Bounds.Center;
        var start = Anchor(source.Bounds, sourcePort, sourceToward);
        var end = Anchor(target.Bounds, targetPort, targetToward);
        return linkType?.Form == DiagramLineForm.Straight
            ? [start.Position, end.Position]
            : Orthogonal(start, end, linkType?.StubLength ?? DefaultStubLength);
    }

    public static LinkAnchor Anchor(DiagramRect bounds, PortPlacement? port, DiagramPoint toward)
    {
        if (port is not null)
        {
            return new LinkAnchor(port.PositionIn(bounds), port.Side);
        }

        var center = bounds.Center;
        var dx = toward.X - center.X;
        var dy = toward.Y - center.Y;
        var side = (Math.Abs(dx) * bounds.Height) >= (Math.Abs(dy) * bounds.Width)
            ? (dx >= 0 ? DiagramSide.Right : DiagramSide.Left)
            : (dy >= 0 ? DiagramSide.Bottom : DiagramSide.Top);
        return new LinkAnchor(PortPosition(bounds, side, 0.5, 0), side);
    }

    public static ImmutableArray<DiagramPoint> Orthogonal(LinkAnchor start, LinkAnchor end, double stub)
    {
        var startOut = Step(start.Position, start.Side, stub);
        var endOut = Step(end.Position, end.Side, stub);
        var points = new List<DiagramPoint>(8) { start.Position, startOut };
        var startHorizontal = IsHorizontal(start.Side);
        var endHorizontal = IsHorizontal(end.Side);
        if (startHorizontal && endHorizontal)
        {
            var middle = (startOut.X + endOut.X) / 2;
            points.Add(startOut with { X = middle });
            points.Add(endOut with { X = middle });
        }
        else if (!startHorizontal && !endHorizontal)
        {
            var middle = (startOut.Y + endOut.Y) / 2;
            points.Add(startOut with { Y = middle });
            points.Add(endOut with { Y = middle });
        }
        else if (startHorizontal)
        {
            points.Add(new DiagramPoint(endOut.X, startOut.Y));
        }
        else
        {
            points.Add(new DiagramPoint(startOut.X, endOut.Y));
        }

        points.Add(endOut);
        points.Add(end.Position);
        return Simplify(points);
    }

    public static string ToPath(ImmutableArray<DiagramPoint> points)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < points.Length; i++)
        {
            builder.Append(i == 0 ? 'M' : 'L')
                .Append(Format(points[i].X))
                .Append(',')
                .Append(Format(points[i].Y));
        }

        return builder.ToString();
    }

    public static string Format(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    private static bool IsHorizontal(DiagramSide side) => side is DiagramSide.Left or DiagramSide.Right;

    private static DiagramPoint Step(DiagramPoint point, DiagramSide side, double length) => side switch
    {
        DiagramSide.Top => point with { Y = point.Y - length },
        DiagramSide.Right => point with { X = point.X + length },
        DiagramSide.Bottom => point with { Y = point.Y + length },
        _ => point with { X = point.X - length }
    };

    private static ImmutableArray<DiagramPoint> Simplify(List<DiagramPoint> points)
    {
        var builder = ImmutableArray.CreateBuilder<DiagramPoint>(points.Count);
        foreach (var point in points)
        {
            if ((builder.Count > 0) && (builder[^1] == point))
            {
                continue;
            }

            if ((builder.Count >= 2) && IsCollinear(builder[^2], builder[^1], point))
            {
                builder[^1] = point;
                continue;
            }

            builder.Add(point);
        }

        return builder.ToImmutable();
    }

    private static bool IsCollinear(DiagramPoint a, DiagramPoint b, DiagramPoint c) =>
        (a.X.Equals(b.X) && b.X.Equals(c.X)) || (a.Y.Equals(b.Y) && b.Y.Equals(c.Y));
}
