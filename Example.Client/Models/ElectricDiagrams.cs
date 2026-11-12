namespace Example.Client.Models;

using System.Collections.Immutable;
using System.Text.Json.Serialization;

public sealed record EquipmentData
{
    public string Maker { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string Serial { get; init; } = string.Empty;

    public int? Made { get; init; }

    public int? Installed { get; init; }

    public double? Capacity { get; init; }

    public DateOnly? NextInspection { get; init; }

    public bool Open { get; init; }
}

public sealed record BusData(int Feeders);

[JsonSerializable(typeof(EquipmentData))]
[JsonSerializable(typeof(BusData))]
public sealed partial class ElectricJsonContext : JsonSerializerContext;

public enum InspectionStatus
{
    Ok,
    Due,
    Expired
}

public static class ElectricDiagrams
{
    public const string Prefix = "el";

    public const string SwitchBadge = "switch";

    public const string InspectionBadge = "inspection";

    private const double SymbolWidth = 40;

    private const double SymbolHeight = 60;

    private const double BusHeight = 8;

    private const double HighVoltageSpacing = 400;

    private const double LowVoltageSpacing = 120;

    private const string Line = "fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"";

    private const string ServiceIcon = $"<path d=\"M20 16v44\" {Line} /><path d=\"M9 3h22L20 16z\" fill=\"currentColor\" />";

    private const string SwitchIcon = $"<path d=\"M20 0v20M20 20l10 18M20 40v20\" {Line} /><circle cx=\"20\" cy=\"40\" r=\"2.5\" fill=\"currentColor\" />";

    private const string DisconnectorIcon = $"<path d=\"M20 0v20M20 20l10 18M14 40h12M20 40v20\" {Line} />";

    private const string BreakerIcon = $"<path d=\"M20 0v20M20 20l10 18M20 40v20M16 36l8 8M24 36l-8 8\" {Line} />";

    private const string TransformerIcon = $"<path d=\"M20 0v10M20 50v10\" {Line} /><circle cx=\"20\" cy=\"21\" r=\"11\" {Line} /><circle cx=\"20\" cy=\"39\" r=\"11\" {Line} />";

    private const string CapacitorIcon = $"<path d=\"M20 0v26M8 26h24M8 34h24M20 34v12M12 46h16M15 51h10M18 56h4\" {Line} />";

    private const string MotorIcon = $"<path d=\"M20 0v22M13 43V29l7 9 7-9v14\" {Line} /><circle cx=\"20\" cy=\"36\" r=\"14\" {Line} />";

    private const string LightingIcon = $"<path d=\"M20 0v24M11.5 27.5l17 17M28.5 27.5l-17 17\" {Line} /><circle cx=\"20\" cy=\"36\" r=\"12\" {Line} />";

    private static readonly DiagramRenderOverride Dead = new() { Opacity = 0.35 };

    public static IReadOnlyList<DiagramBadgeKind> BadgeKinds { get; } =
    [
        new() { Name = SwitchBadge, Corner = DiagramCorner.TopLeft },
        new() { Name = InspectionBadge, Corner = DiagramCorner.TopRight }
    ];

    public static DiagramSchema Schema { get; } = new()
    {
        NodeTypes =
        [
            Equipment("service", "受電点", "receiving", "service", [Out("hv")]) with { Start = true },
            Equipment("pas", "区分開閉器", "switch", "switch", [In("hv"), Out("hv")]),
            Equipment("ds", "断路器", "switch", "disconnector", [In("hv"), Out("hv")]),
            Equipment("vcb", "遮断器", "switch", "breaker", [In("hv"), Out("hv")]),
            Bus("hv-bus", "高圧母線", "hv"),
            Equipment("transformer", "変圧器", "transformer", "transformer", [In("hv"), Out("lv")]),
            Equipment("capacitor", "進相コンデンサ", "transformer", "capacitor", [In("hv")]),
            Bus("lv-bus", "低圧母線", "lv"),
            Equipment("mccb", "配線用遮断器", "switch", "breaker", [In("lv"), Out("lv")]),
            Equipment("motor", "動力負荷", "load", "motor", [In("lv")]),
            Equipment("lighting", "電灯負荷", "load", "lighting", [In("lv")])
        ],
        Categories =
        [
            new DiagramCategory { Name = "receiving", DisplayName = "受電" },
            new DiagramCategory { Name = "switch", DisplayName = "開閉器・遮断器" },
            new DiagramCategory { Name = "transformer", DisplayName = "変圧器・コンデンサ" },
            new DiagramCategory { Name = "bus", DisplayName = "母線" },
            new DiagramCategory { Name = "load", DisplayName = "負荷" }
        ],
        LinkTypes =
        [
            new DiagramLinkType { Name = "hv", TargetMarker = DiagramMarker.None, Look = new DiagramLook { Stroke = "var(--example-electric-line)", StrokeWidth = 3 } },
            new DiagramLinkType { Name = "lv", TargetMarker = DiagramMarker.None, Look = new DiagramLook { Stroke = "var(--example-electric-line)", StrokeWidth = 1.5 } }
        ],
        DefaultLinkType = "lv",
        IconSets =
        [
            new DiagramIconSet
            {
                Prefix = Prefix,
                Icons =
                [
                    Icon("service", ServiceIcon),
                    Icon("switch", SwitchIcon),
                    Icon("disconnector", DisconnectorIcon),
                    Icon("breaker", BreakerIcon),
                    Icon("transformer", TransformerIcon),
                    Icon("capacitor", CapacitorIcon),
                    Icon("motor", MotorIcon),
                    Icon("lighting", LightingIcon)
                ]
            }
        ]
    };

    public static double WidthOf(string type, int feeders) =>
        feeders * (string.Equals(type, "hv-bus", StringComparison.Ordinal) ? HighVoltageSpacing : LowVoltageSpacing);

    public static bool IsSwitch(string type) => type is "pas" or "ds" or "vcb" or "mccb";

    public static string? CapacityLabel(string type) => type switch
    {
        "transformer" or "motor" or "lighting" => "容量 (kVA)",
        "capacitor" => "容量 (kvar)",
        "pas" or "ds" or "vcb" or "mccb" => "定格電流 (A)",
        _ => null
    };

    public static InspectionStatus StatusOf(EquipmentData? data, DateOnly today)
    {
        if (data?.NextInspection is not { } next)
        {
            return InspectionStatus.Ok;
        }

        if (next < today)
        {
            return InspectionStatus.Expired;
        }

        return next <= today.AddDays(30) ? InspectionStatus.Due : InspectionStatus.Ok;
    }

    public static Diagram Create(DateOnly today)
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("service", "service", 580, 0, "受電点", new EquipmentData()));
        diagram.Add(Node("pas", "pas", 580, 100, "PAS", new EquipmentData { Maker = "A 電機", Model = "PAS-200", Serial = "P-1001", Made = 2016, Installed = 2017, Capacity = 200, NextInspection = today.AddDays(200) }));
        diagram.Add(Node("ds", "ds", 580, 200, "DS", new EquipmentData { Maker = "A 電機", Model = "DS-400", Serial = "D-2001", Made = 2016, Installed = 2017, Capacity = 400, NextInspection = today.AddDays(200) }));
        diagram.Add(Node("vcb", "vcb", 580, 300, "VCB", new EquipmentData { Maker = "B 電機", Model = "VCB-7.2", Serial = "V-3001", Made = 2018, Installed = 2018, Capacity = 600, NextInspection = today.AddDays(14) }));
        diagram.Add(BusNode("hv-bus", "hv-bus", 0, 416, 3, "高圧母線"));
        diagram.Add(Node("tr1", "transformer", 180, 460, "TR1 電灯", new EquipmentData { Maker = "C 変圧器", Model = "TR-1P-50", Serial = "T-4001", Made = 2015, Installed = 2017, Capacity = 50, NextInspection = today.AddDays(120) }));
        diagram.Add(Node("tr2", "transformer", 580, 460, "TR2 動力", new EquipmentData { Maker = "C 変圧器", Model = "TR-3P-50", Serial = "T-4002", Made = 2015, Installed = 2017, Capacity = 50, NextInspection = today.AddDays(-10) }));
        diagram.Add(Node("sc", "capacitor", 980, 460, "SC", new EquipmentData { Maker = "D 電機", Model = "SC-30", Serial = "C-5001", Made = 2019, Installed = 2019, Capacity = 30 }));
        diagram.Add(BusNode("lv-bus1", "lv-bus", 80, 576, 2, "低圧 1"));
        diagram.Add(BusNode("lv-bus2", "lv-bus", 420, 576, 3, "低圧 2"));
        diagram.Add(Node("mccb-l1", "mccb", 120, 620, "MCCB 1", new EquipmentData { Maker = "B 電機", Model = "MC-100", Capacity = 100 }));
        diagram.Add(Node("mccb-l2", "mccb", 240, 620, "MCCB 2", new EquipmentData { Maker = "B 電機", Model = "MC-100", Capacity = 100 }));
        diagram.Add(Node("mccb-m1", "mccb", 460, 620, "MCCB 3", new EquipmentData { Maker = "B 電機", Model = "MC-150", Capacity = 150 }));
        diagram.Add(Node("mccb-m2", "mccb", 580, 620, "MCCB 4", new EquipmentData { Maker = "B 電機", Model = "MC-150", Capacity = 150 }));
        diagram.Add(Node("mccb-s", "mccb", 700, 620, "予備", new EquipmentData { Maker = "B 電機", Model = "MC-100", Capacity = 100 }));
        diagram.Add(Node("l1", "lighting", 120, 740, "照明 1F", new EquipmentData { Capacity = 20 }));
        diagram.Add(Node("l2", "lighting", 240, 740, "照明 2F", new EquipmentData { Capacity = 15 }));
        diagram.Add(Node("m1", "motor", 460, 740, "空調", new EquipmentData { Maker = "E 機械", Capacity = 30 }));
        diagram.Add(Node("m2", "motor", 580, 740, "ポンプ", new EquipmentData { Maker = "E 機械", Capacity = 30 }));
        diagram.Connect(Link("service-pas", "service", "out", "pas", "in", "hv"));
        diagram.Connect(Link("pas-ds", "pas", "out", "ds", "in", "hv"));
        diagram.Connect(Link("ds-vcb", "ds", "out", "vcb", "in", "hv"));
        diagram.Connect(Link("vcb-bus", "vcb", "out", "hv-bus", "in", "hv"));
        diagram.Connect(Link("bus-tr1", "hv-bus", "f1", "tr1", "in", "hv"));
        diagram.Connect(Link("bus-tr2", "hv-bus", "f2", "tr2", "in", "hv"));
        diagram.Connect(Link("bus-sc", "hv-bus", "f3", "sc", "in", "hv"));
        diagram.Connect(Link("tr1-bus", "tr1", "out", "lv-bus1", "in", "lv"));
        diagram.Connect(Link("tr2-bus", "tr2", "out", "lv-bus2", "in", "lv"));
        diagram.Connect(Link("bus1-l1", "lv-bus1", "f1", "mccb-l1", "in", "lv"));
        diagram.Connect(Link("bus1-l2", "lv-bus1", "f2", "mccb-l2", "in", "lv"));
        diagram.Connect(Link("bus2-m1", "lv-bus2", "f1", "mccb-m1", "in", "lv"));
        diagram.Connect(Link("bus2-m2", "lv-bus2", "f2", "mccb-m2", "in", "lv"));
        diagram.Connect(Link("bus2-s", "lv-bus2", "f3", "mccb-s", "in", "lv"));
        diagram.Connect(Link("mccb-l1-l1", "mccb-l1", "out", "l1", "in", "lv"));
        diagram.Connect(Link("mccb-l2-l2", "mccb-l2", "out", "l2", "in", "lv"));
        diagram.Connect(Link("mccb-m1-m1", "mccb-m1", "out", "m1", "in", "lv"));
        diagram.Connect(Link("mccb-m2-m2", "mccb-m2", "out", "m2", "in", "lv"));
        diagram.ClearHistory();
        return diagram;
    }

    public static ImmutableArray<DiagramIssue> Check(Diagram diagram)
    {
        var snapshot = diagram.Snapshot;
        var issues = ImmutableArray.CreateBuilder<DiagramIssue>();
        issues.AddRange(diagram.Validate());
        issues.AddRange(snapshot.GetUnreachable(diagram.Schema).Select(static x => new DiagramIssue(DiagramIssueSeverity.Warning, DiagramIssueKind.App, "受電点からつながっていません") { ElementIds = [x.Id] }));
        foreach (var transformer in snapshot.Nodes.Where(static x => string.Equals(x.Type, "transformer", StringComparison.Ordinal)))
        {
            if (transformer.Data is not EquipmentData { Capacity: { } capacity })
            {
                continue;
            }

            var load = Downstream(snapshot, transformer.Id).Sum(static x => x is { Type: "motor" or "lighting", Data: EquipmentData { Capacity: { } value } } ? value : 0);
            if (load > capacity)
            {
                issues.Add(new DiagramIssue(DiagramIssueSeverity.Warning, DiagramIssueKind.App, string.Create(CultureInfo.InvariantCulture, $"負荷の合計 {load} kVA が容量 {capacity} kVA を超えています")) { ElementIds = [transformer.Id] });
            }
        }

        return issues.ToImmutable();
    }

    public static Dictionary<string, DiagramOverlay> Overlays(Diagram diagram, DateOnly today)
    {
        var snapshot = diagram.Snapshot;
        var (nodes, links) = Energized(diagram);
        var result = new Dictionary<string, DiagramOverlay>(StringComparer.Ordinal);
        foreach (var node in snapshot.Nodes)
        {
            var badges = ImmutableArray.CreateBuilder<DiagramBadge>();
            if (node.Data is EquipmentData data)
            {
                if (data.Open)
                {
                    badges.Add(new DiagramBadge { Kind = SwitchBadge, Text = "切", Tone = DiagramBadgeTone.Warning });
                }

                var status = StatusOf(data, today);
                if (status == InspectionStatus.Expired)
                {
                    badges.Add(new DiagramBadge { Kind = InspectionBadge, Text = "点検期限切れ", Tone = DiagramBadgeTone.Error });
                }
                else if ((status == InspectionStatus.Due) && (data.NextInspection is { } next))
                {
                    badges.Add(new DiagramBadge { Kind = InspectionBadge, Text = string.Create(CultureInfo.InvariantCulture, $"点検 {next:M/d}"), Tone = DiagramBadgeTone.Warning });
                }
            }

            var dead = !nodes.Contains(node.Id);
            if (dead || (badges.Count > 0))
            {
                result[node.Id] = new DiagramOverlay { Badges = badges.ToImmutable(), Style = dead ? Dead : null };
            }
        }

        foreach (var link in snapshot.Links.Where(x => !links.Contains(x.Id)))
        {
            result[link.Id] = new DiagramOverlay { Style = Dead };
        }

        return result;
    }

    private static (HashSet<string> Nodes, HashSet<string> Links) Energized(Diagram diagram)
    {
        var snapshot = diagram.Snapshot;
        var starts = diagram.Schema.NodeTypes.Where(static x => x.Start).Select(static x => x.Name).ToHashSet(StringComparer.Ordinal);
        var nodes = new HashSet<string>(StringComparer.Ordinal);
        var links = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(snapshot.Nodes.Where(x => starts.Contains(x.Type)).Select(static x => x.Id));
        while (queue.TryDequeue(out var id))
        {
            if (!nodes.Add(id) || (snapshot.Find(id) is DiagramNode { Data: EquipmentData { Open: true } }))
            {
                continue;
            }

            foreach (var link in snapshot.GetLinks(id, DiagramLinkDirection.Out))
            {
                links.Add(link.Id);
                queue.Enqueue(link.Target.ElementId);
            }
        }

        return (nodes, links);
    }

    private static IEnumerable<DiagramNode> Downstream(DiagramSnapshot snapshot, string id)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { id };
        var queue = new Queue<string>([id]);
        while (queue.TryDequeue(out var current))
        {
            foreach (var link in snapshot.GetLinks(current, DiagramLinkDirection.Out))
            {
                if (visited.Add(link.Target.ElementId) && (snapshot.Find(link.Target.ElementId) is DiagramNode node))
                {
                    queue.Enqueue(node.Id);
                    yield return node;
                }
            }
        }
    }

    private static DiagramNodeType Equipment(string name, string displayName, string category, string icon, ImmutableArray<DiagramPortDefinition> ports) => new()
    {
        Name = name,
        DisplayName = displayName,
        Category = category,
        Size = new DiagramSize(SymbolWidth, SymbolHeight),
        Permissions = DiagramPermissions.All & ~DiagramPermissions.Resize,
        Look = new DiagramLook { Icon = $"{Prefix}:{icon}", Fill = "transparent", Stroke = "none" },
        Slots = [new DiagramLabelSlot { Name = "name", Placement = DiagramLabelPlacement.After, Width = 60, MaxLines = 3 }],
        Ports = ports,
        DataType = ElectricJsonContext.Default.EquipmentData,
        DefaultData = new EquipmentData(),
        CreateContent = _ => new DiagramContent { Label = displayName, Data = new EquipmentData() }
    };

    private static DiagramNodeType Bus(string name, string displayName, string kind) => new()
    {
        Name = name,
        DisplayName = displayName,
        Category = "bus",
        Size = new DiagramSize(WidthOf(name, 2), BusHeight),
        Permissions = DiagramPermissions.All & ~DiagramPermissions.Resize,
        Look = new DiagramLook { Fill = "var(--example-electric-line)", Stroke = "none" },
        Slots = [new DiagramLabelSlot { Name = "name", Placement = DiagramLabelPlacement.Before, Width = 70, MaxLines = 2 }],
        DataType = ElectricJsonContext.Default.BusData,
        DefaultData = new BusData(2),
        GetPorts = box => box.Data is BusData bus ? BusPorts(bus, kind) : [],
        CreateContent = _ => new DiagramContent { Label = displayName, Data = new BusData(2) }
    };

    private static ImmutableArray<DiagramPortDefinition> BusPorts(BusData bus, string kind) =>
        [
            In(kind) with { MaxLinks = null },
            .. Enumerable.Range(0, bus.Feeders).Select(i => Out(kind) with { Id = string.Create(CultureInfo.InvariantCulture, $"f{i + 1}"), Ratio = (i + 0.5) / bus.Feeders })
        ];

    private static DiagramPortDefinition In(string kind) =>
        new() { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Top, Kind = kind, LinkType = kind, MaxLinks = 1, Required = true, Visibility = DiagramPortVisibility.WhileConnecting };

    private static DiagramPortDefinition Out(string kind) =>
        new() { Id = "out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Bottom, Kind = kind, LinkType = kind, MaxLinks = 1 };

    private static DiagramIcon Icon(string id, string body) => new() { Id = id, Body = body, Width = SymbolWidth, Height = SymbolHeight };

    private static DiagramNode Node(string id, string type, double x, double y, string label, EquipmentData data) =>
        new() { Id = id, Type = type, Bounds = new DiagramRect(x, y, SymbolWidth, SymbolHeight), Label = label, Data = data };

    private static DiagramNode BusNode(string id, string type, double x, double y, int feeders, string label) =>
        new() { Id = id, Type = type, Bounds = new DiagramRect(x, y, WidthOf(type, feeders), BusHeight), Label = label, Data = new BusData(feeders) };

    private static DiagramLink Link(string id, string source, string sourcePort, string target, string targetPort, string type) =>
        new() { Id = id, Type = type, Source = new DiagramLinkEnd(source, sourcePort), Target = new DiagramLinkEnd(target, targetPort) };
}
