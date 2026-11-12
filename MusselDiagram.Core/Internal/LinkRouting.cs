namespace MusselDiagram.Internal;

using System.Collections.Immutable;
using System.Globalization;
using System.Text;

internal readonly record struct LinkAnchor(DiagramPoint Position, DiagramSide Side);

internal sealed record PortPlacement(DiagramPortDefinition Definition, double Ratio, double? Along = null)
{
    public string Id => Definition.Id;

    public DiagramSide Side => Definition.Side;

    public double Offset => Definition.Offset;

    public DiagramPoint PositionIn(DiagramRect bounds) => LinkRouting.PortPosition(bounds, Side, Ratio, Offset, Along);
}

internal static class LinkRouting
{
    public const double DefaultStubLength = 20;

    public const double DefaultObstacleMargin = 10;

    public const double MarkerLength = 10;

    public static DiagramPoint PortPosition(DiagramRect bounds, DiagramSide side, double ratio, double offset, double? along = null) => side switch
    {
        DiagramSide.Top => new(bounds.X + (along ?? (bounds.Width * ratio)), bounds.Y - offset),
        DiagramSide.Right => new(bounds.Right + offset, bounds.Y + (along ?? (bounds.Height * ratio))),
        DiagramSide.Bottom => new(bounds.X + (along ?? (bounds.Width * ratio)), bounds.Bottom + offset),
        _ => new(bounds.X - offset, bounds.Y + (along ?? (bounds.Height * ratio)))
    };

    public static ImmutableArray<PortPlacement> Place(ImmutableArray<DiagramPortDefinition> ports, DiagramBoxType? type = null)
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

            var along = (port.Row is { } row) && (port.Side is DiagramSide.Left or DiagramSide.Right)
                ? (type?.RowHeaderHeight ?? 0) + ((row + 0.5) * (type?.RowHeight ?? 20))
                : (double?)null;
            builder.Add(new PortPlacement(port, ratio.Value, along));
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

    public static (LinkAnchor Start, LinkAnchor End) Anchors(DiagramRect source, PortPlacement? sourcePort, DiagramRect target, PortPlacement? targetPort)
    {
        var sourceToward = targetPort?.PositionIn(target) ?? target.Center;
        var targetToward = sourcePort?.PositionIn(source) ?? source.Center;
        return (Anchor(source, sourcePort, sourceToward), Anchor(target, targetPort, targetToward));
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

    public static ImmutableArray<DiagramPoint> Simple(LinkAnchor start, LinkAnchor end, DiagramLinkType? linkType)
    {
        var stub = linkType?.StubLength ?? DefaultStubLength;
        return (linkType?.Form ?? DiagramLineForm.Orthogonal) switch
        {
            DiagramLineForm.Straight => [start.Position, end.Position],
            DiagramLineForm.Curved => Curve(start, end, stub),
            _ => Orthogonal(start, end, stub)
        };
    }

    public static ImmutableArray<DiagramPoint> Route(LinkAnchor start, LinkAnchor end, DiagramLinkType? linkType, ObstacleIndex obstacles, DiagramBox source, DiagramBox target)
    {
        if ((linkType?.Form ?? DiagramLineForm.Orthogonal) != DiagramLineForm.Orthogonal)
        {
            return Simple(start, end, linkType);
        }

        return OrthogonalRouter.Route(
            start,
            end,
            linkType?.StubLength ?? DefaultStubLength,
            linkType?.ObstacleMargin ?? DefaultObstacleMargin,
            obstacles,
            source.Id,
            target.Id,
            source.Bounds,
            target.Bounds);
    }

    public static ImmutableArray<DiagramPoint> Curve(LinkAnchor start, LinkAnchor end, double stub)
    {
        var dx = end.Position.X - start.Position.X;
        var dy = end.Position.Y - start.Position.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        var reach = Math.Max(stub, distance / 2);
        return [start.Position, Step(start.Position, start.Side, reach), Step(end.Position, end.Side, reach), end.Position];
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

    public static string ToPath(ImmutableArray<DiagramPoint> points, DiagramLineForm form, double sourceCut, double targetCut)
    {
        if (points.Length < 2)
        {
            return string.Empty;
        }

        var first = Cut(points[0], points[1], sourceCut);
        var last = Cut(points[^1], points[^2], targetCut);
        var builder = new StringBuilder();
        Append(builder, 'M', first);
        if ((form == DiagramLineForm.Curved) && (points.Length == 4))
        {
            Append(builder, 'C', points[1]);
            builder.Append(' ');
            Append(builder, null, points[2]);
            builder.Append(' ');
            Append(builder, null, last);
            return builder.ToString();
        }

        for (var i = 1; i < points.Length - 1; i++)
        {
            Append(builder, 'L', points[i]);
        }

        Append(builder, 'L', last);
        return builder.ToString();
    }

    public static (double Source, double Target) Cuts(DiagramLinkType? type) =>
        (type is null)
            ? (0, MarkerLength)
            : (type.SourceMarker == DiagramMarker.None ? 0 : MarkerLength, type.TargetMarker == DiagramMarker.None ? 0 : MarkerLength);

    public static double Angle(DiagramPoint from, DiagramPoint to) =>
        Math.Round(Math.Atan2(to.Y - from.Y, to.X - from.X) * 180 / Math.PI, 2);

    public static string Format(double value)
    {
        Span<char> buffer = stackalloc char[24];
        return new string(buffer[..Write(buffer, value)]);
    }

    public static DiagramPoint Step(DiagramPoint point, DiagramSide side, double length) => side switch
    {
        DiagramSide.Top => point with { Y = point.Y - length },
        DiagramSide.Right => point with { X = point.X + length },
        DiagramSide.Bottom => point with { Y = point.Y + length },
        _ => point with { X = point.X - length }
    };

    public static bool IsHorizontal(DiagramSide side) => side is DiagramSide.Left or DiagramSide.Right;

    public static ImmutableArray<DiagramPoint> Simplify(List<DiagramPoint> points)
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

    private static DiagramPoint Cut(DiagramPoint end, DiagramPoint toward, double length)
    {
        if (length <= 0)
        {
            return end;
        }

        var dx = toward.X - end.X;
        var dy = toward.Y - end.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance <= 0)
        {
            return end;
        }

        var ratio = Math.Min(length, distance) / distance;
        return new DiagramPoint(end.X + (dx * ratio), end.Y + (dy * ratio));
    }

    private static void Append(StringBuilder builder, char? command, DiagramPoint point)
    {
        if (command is { } value)
        {
            builder.Append(value);
        }

        Span<char> buffer = stackalloc char[24];
        builder.Append(buffer[..Write(buffer, point.X)]);
        builder.Append(',');
        builder.Append(buffer[..Write(buffer, point.Y)]);
    }

    private static int Write(Span<char> buffer, double value)
    {
        int written;
        if (!(Math.Abs(value) < 1e15))
        {
            (Math.Floor((value * 100) + 0.5) / 100).TryFormat(buffer, out written, default, CultureInfo.InvariantCulture);
            return written;
        }

        var scaled = (long)Math.Floor((value * 100) + 0.5);
        var length = 0;
        if (scaled < 0)
        {
            buffer[length++] = '-';
            scaled = -scaled;
        }

        (scaled / 100).TryFormat(buffer[length..], out written, default, CultureInfo.InvariantCulture);
        length += written;
        var fraction = (int)(scaled % 100);
        if (fraction != 0)
        {
            buffer[length++] = '.';
            buffer[length++] = (char)('0' + (fraction / 10));
            if ((fraction % 10) != 0)
            {
                buffer[length++] = (char)('0' + (fraction % 10));
            }
        }

        return length;
    }

    private static bool IsCollinear(DiagramPoint a, DiagramPoint b, DiagramPoint c) =>
        (a.X.Equals(b.X) && b.X.Equals(c.X)) || (a.Y.Equals(b.Y) && b.Y.Equals(c.Y));
}
