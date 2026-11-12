namespace Example.Client.Models;

using System.Text.Json.Serialization;

public sealed record DeviceData(string Address);

public sealed record DeviceMetric(string Name, string Unit, double Low, double High, double Warning);

public sealed record DeviceReading(string State, double Value, IReadOnlyList<double> History);

[JsonSerializable(typeof(DeviceData))]
public sealed partial class MonitorJsonContext : JsonSerializerContext;

public static class MonitorDiagrams
{
    public const string Prefix = "mon";

    private const string RouterIcon = "<rect x=\"3\" y=\"9\" width=\"18\" height=\"8\" rx=\"2\" fill=\"#0EA5E9\" /><path d=\"M7 9 9 5M17 9l-2-4\" stroke=\"#0EA5E9\" stroke-width=\"2\" stroke-linecap=\"round\" /><circle cx=\"7\" cy=\"13\" r=\"1.2\" fill=\"#FFFFFF\" /><circle cx=\"11\" cy=\"13\" r=\"1.2\" fill=\"#FFFFFF\" />";

    private const string SwitchIcon = "<rect x=\"2\" y=\"8\" width=\"20\" height=\"8\" rx=\"1.5\" fill=\"#6366F1\" /><path d=\"M5 12h2M9 12h2M13 12h2M17 12h2\" stroke=\"#FFFFFF\" stroke-width=\"2\" />";

    private const string ServerIcon = "<rect x=\"4\" y=\"3\" width=\"16\" height=\"8\" rx=\"1.5\" fill=\"#ED7100\" /><rect x=\"4\" y=\"13\" width=\"16\" height=\"8\" rx=\"1.5\" fill=\"#ED7100\" /><circle cx=\"8\" cy=\"7\" r=\"1.2\" fill=\"#FFFFFF\" /><circle cx=\"8\" cy=\"17\" r=\"1.2\" fill=\"#FFFFFF\" />";

    private const string ControllerIcon = "<rect x=\"6\" y=\"6\" width=\"12\" height=\"12\" rx=\"1.5\" fill=\"#10B981\" /><path d=\"M9 3v3M12 3v3M15 3v3M9 18v3M12 18v3M15 18v3M3 9h3M3 12h3M3 15h3M18 9h3M18 12h3M18 15h3\" stroke=\"#10B981\" stroke-width=\"1.6\" />";

    private const string SensorIcon = "<path d=\"M10 4a2 2 0 0 1 4 0v9a4 4 0 1 1-4 0z\" fill=\"none\" stroke=\"#EF4444\" stroke-width=\"2\" /><circle cx=\"12\" cy=\"16\" r=\"2.2\" fill=\"#EF4444\" />";

    public static IReadOnlyList<DiagramBadgeKind> BadgeKinds { get; } =
    [
        new() { Name = "status", Corner = DiagramCorner.TopLeft },
        new() { Name = "value", Corner = DiagramCorner.TopRight }
    ];

    public static IReadOnlyDictionary<string, DeviceMetric> Metrics { get; } = new Dictionary<string, DeviceMetric>(StringComparer.Ordinal)
    {
        ["router"] = new("通信量", "Mbps", 100, 900, 760),
        ["switch"] = new("通信量", "Mbps", 50, 600, 520),
        ["server"] = new("CPU", "%", 5, 100, 85),
        ["controller"] = new("周期", "ms", 8, 30, 25),
        ["sensor"] = new("温度", "℃", 20, 90, 72)
    };

    public static DiagramSchema Schema { get; } = new()
    {
        NodeTypes =
        [
            Device("router", "ルーター"),
            Device("switch", "スイッチ"),
            Device("server", "サーバー"),
            Device("controller", "制御装置"),
            Device("sensor", "温度センサー")
        ],
        GroupTypes = [new DiagramGroupType { Name = "area", DisplayName = "エリア", Look = new DiagramLook { Stroke = "var(--example-node-stroke)", Dash = DiagramDash.Dashed } }],
        LinkTypes = [new DiagramLinkType { Name = "cable", TargetMarker = DiagramMarker.None, Look = new DiagramLook { Stroke = "var(--example-flow-line)" } }],
        DefaultLinkType = "cable",
        IconSets =
        [
            new DiagramIconSet
            {
                Prefix = Prefix,
                Icons =
                [
                    Icon("router", RouterIcon),
                    Icon("switch", SwitchIcon),
                    Icon("server", ServerIcon),
                    Icon("controller", ControllerIcon),
                    Icon("sensor", SensorIcon)
                ]
            }
        ]
    };

    public static Diagram Create()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramGroup { Id = "room", Type = "area", Bounds = new DiagramRect(20, 20, 440, 290), Label = "サーバールーム" });
        diagram.Add(new DiagramGroup { Id = "line", Type = "area", Bounds = new DiagramRect(520, 20, 440, 290), Label = "製造ライン 1" });
        diagram.Add(Node("router", "router", 70, 80, "room", "コアルーター", "10.0.0.1"));
        diagram.Add(Node("switch", "switch", 250, 80, "room", "スイッチ", "10.0.0.2"));
        diagram.Add(Node("server-a", "server", 160, 200, "room", "業務サーバー", "10.0.1.10"));
        diagram.Add(Node("server-b", "server", 330, 200, "room", "記録サーバー", "10.0.1.11"));
        diagram.Add(Node("plc", "controller", 600, 80, "line", "ライン制御", "10.0.2.1"));
        diagram.Add(Node("sensor-1", "sensor", 560, 200, "line", "炉 1", "10.0.2.11"));
        diagram.Add(Node("sensor-2", "sensor", 700, 200, "line", "炉 2", "10.0.2.12"));
        diagram.Add(Node("sensor-3", "sensor", 840, 200, "line", "冷却", "10.0.2.13"));
        diagram.Connect(Cable("router-switch", "router", "switch"));
        diagram.Connect(Cable("switch-server-a", "switch", "server-a"));
        diagram.Connect(Cable("switch-server-b", "switch", "server-b"));
        diagram.Connect(Cable("switch-plc", "switch", "plc"));
        diagram.Connect(Cable("plc-sensor-1", "plc", "sensor-1"));
        diagram.Connect(Cable("plc-sensor-2", "plc", "sensor-2"));
        diagram.Connect(Cable("plc-sensor-3", "plc", "sensor-3"));
        diagram.ClearHistory();
        return diagram;
    }

    private static DiagramNodeType Device(string name, string displayName) => new()
    {
        Name = name,
        DisplayName = displayName,
        Size = new DiagramSize(60, 60),
        Look = new DiagramLook { Icon = $"{Prefix}:{name}", Fill = "var(--example-node-fill)", Stroke = "var(--example-node-stroke)" },
        Slots = [new DiagramLabelSlot { Name = "name", Placement = DiagramLabelPlacement.Below, Width = 120, MaxLines = 2 }],
        DataType = MonitorJsonContext.Default.DeviceData,
        GetTooltip = static box => box.Data is DeviceData data ? $"{box.Label}\n{data.Address}" : box.Label
    };

    private static DiagramIcon Icon(string id, string body) =>
        new() { Id = id, Body = $"<g transform=\"translate(6 6)\">{body}</g>", Width = 36, Height = 36 };

    private static DiagramNode Node(string id, string type, double x, double y, string parent, string label, string address) =>
        new() { Id = id, Type = type, Bounds = new DiagramRect(x, y, 60, 60), ParentId = parent, Label = label, Data = new DeviceData(address) };

    private static DiagramLink Cable(string id, string source, string target) =>
        new() { Id = id, Type = "cable", Source = new DiagramLinkEnd(source), Target = new DiagramLinkEnd(target) };
}
