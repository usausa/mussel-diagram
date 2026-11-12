namespace Example.Client.Models;

public sealed class MonitorSimulation(Diagram diagram)
{
    private const int HistoryLength = 8;

    private readonly Dictionary<string, DeviceReading> readings = new(StringComparer.Ordinal);

    private int tick;

    public static string Format(double value, DeviceMetric metric) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:0} {metric.Unit}");

    public DeviceReading? ReadingOf(string id) => readings.GetValueOrDefault(id);

    public Dictionary<string, DiagramOverlay> Next()
    {
        tick++;
        var overlays = new Dictionary<string, DiagramOverlay>(StringComparer.Ordinal);
        var nodes = diagram.Snapshot.Nodes;
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (!MonitorDiagrams.Metrics.TryGetValue(node.Type, out var metric))
            {
                continue;
            }

            var down = string.Equals(node.Id, "sensor-2", StringComparison.Ordinal) && (tick % 14 is >= 10 and <= 12);
            var wave = 0.5 + (0.35 * Math.Sin((tick * 0.7) + i)) + (0.15 * Math.Sin((tick * 1.9) + (2 * i)));
            var value = down ? 0 : Math.Round(metric.Low + ((metric.High - metric.Low) * wave));
            var state = down ? "down" : value >= metric.Warning ? "warning" : "ok";
            IReadOnlyList<double> history = readings.TryGetValue(node.Id, out var previous) ? [.. previous.History.Append(value).TakeLast(HistoryLength)] : [value];
            readings[node.Id] = new DeviceReading(state, value, history);
            overlays[node.Id] = new DiagramOverlay
            {
                States = [state],
                Style = down ? new DiagramRenderOverride { Opacity = 0.5 } : null,
                Badges = [.. Badges(state, down ? "—" : Format(value, metric))]
            };
        }

        foreach (var link in diagram.Snapshot.Links)
        {
            if (readings.GetValueOrDefault(link.Target.ElementId) is { State: "down" })
            {
                overlays[link.Id] = new DiagramOverlay { States = ["cut"] };
            }
            else if ((readings.GetValueOrDefault(link.Source.ElementId) is { } source) && MonitorDiagrams.Metrics.TryGetValue(diagram.Find(link.Source.ElementId)?.Type ?? string.Empty, out var metric) && (source.Value >= metric.Warning))
            {
                overlays[link.Id] = new DiagramOverlay { States = ["busy"] };
            }
        }

        return overlays;
    }

    private static IEnumerable<DiagramBadge> Badges(string state, string value)
    {
        if (string.Equals(state, "warning", StringComparison.Ordinal))
        {
            yield return new DiagramBadge { Kind = "status", Text = "注意", Tone = DiagramBadgeTone.Warning };
        }
        else if (string.Equals(state, "down", StringComparison.Ordinal))
        {
            yield return new DiagramBadge { Kind = "status", Text = "停止", Tone = DiagramBadgeTone.Error };
        }

        yield return new DiagramBadge { Kind = "value", Text = value };
    }
}
