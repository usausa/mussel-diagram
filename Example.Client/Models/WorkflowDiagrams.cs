namespace Example.Client.Models;

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

public sealed record TriggerSettings(string Schedule);

public sealed record HttpSettings(string Method, string Address);

public sealed record BranchSettings(string Condition);

public sealed record AgentSettings(string Prompt);

public sealed record ModelSettings(string Model);

public sealed record ToolSettings(string Description);

public sealed record NotifySettings(string Channel);

[JsonSerializable(typeof(TriggerSettings))]
[JsonSerializable(typeof(HttpSettings))]
[JsonSerializable(typeof(BranchSettings))]
[JsonSerializable(typeof(AgentSettings))]
[JsonSerializable(typeof(ModelSettings))]
[JsonSerializable(typeof(ToolSettings))]
[JsonSerializable(typeof(NotifySettings))]
public sealed partial class WorkflowJsonContext : JsonSerializerContext;

public static class WorkflowDiagrams
{
    public const string Prefix = "wf";

    public const string StatusBadge = "status";

    public const string RunsBadge = "runs";

    private const string TriggerIcon = "<path d=\"M13 2 4 14h7l-1 8 9-12h-7z\" fill=\"#F59E0B\" />";

    private const string HttpIcon = "<circle cx=\"12\" cy=\"12\" r=\"9\" fill=\"none\" stroke=\"#2563EB\" stroke-width=\"2\" /><path d=\"M3 12h18M12 3c3 3.5 3 14.5 0 18M12 3c-3 3.5-3 14.5 0 18\" fill=\"none\" stroke=\"#2563EB\" stroke-width=\"1.6\" />";

    private const string BranchIcon = "<path d=\"M6 5v5c0 3 3 4 6 4s6 1 6 4v1M18 5v5\" fill=\"none\" stroke=\"#0EA5E9\" stroke-width=\"2\" stroke-linecap=\"round\" /><circle cx=\"6\" cy=\"4\" r=\"2\" fill=\"#0EA5E9\" /><circle cx=\"18\" cy=\"4\" r=\"2\" fill=\"#0EA5E9\" /><circle cx=\"18\" cy=\"20\" r=\"2\" fill=\"#0EA5E9\" />";

    private const string AgentIcon = "<rect x=\"5\" y=\"8\" width=\"14\" height=\"11\" rx=\"2\" fill=\"#7C3AED\" /><circle cx=\"9.5\" cy=\"13\" r=\"1.5\" fill=\"#FFFFFF\" /><circle cx=\"14.5\" cy=\"13\" r=\"1.5\" fill=\"#FFFFFF\" /><path d=\"M12 4v4\" stroke=\"#7C3AED\" stroke-width=\"2\" /><circle cx=\"12\" cy=\"4\" r=\"1.5\" fill=\"#7C3AED\" />";

    private const string ModelIcon = "<path d=\"M12 3l2 6 6 2-6 2-2 6-2-6-6-2 6-2z\" fill=\"#A855F7\" />";

    private const string ToolIcon = "<path d=\"M14.7 6.3a4 4 0 0 0-5.4 5.4L4 17l3 3 5.3-5.3a4 4 0 0 0 5.4-5.4l-2.4 2.4-2.6-.6-.6-2.6z\" fill=\"#64748B\" />";

    private const string NotifyIcon = "<path d=\"M12 3a5 5 0 0 0-5 5v4l-2 3h14l-2-3V8a5 5 0 0 0-5-5zM10 18a2 2 0 0 0 4 0\" fill=\"#10B981\" />";

    private static readonly DiagramPortDefinition MainIn = new() { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left, Kind = "main" };

    private static readonly DiagramPortDefinition MainOut = new() { Id = "out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right, Kind = "main" };

    public static IReadOnlyList<DiagramBadgeKind> BadgeKinds { get; } =
    [
        new() { Name = StatusBadge, Corner = DiagramCorner.TopLeft },
        new() { Name = RunsBadge, Corner = DiagramCorner.BottomRight, Clickable = true }
    ];

    public static DiagramSchema Schema { get; } = new()
    {
        NodeTypes =
        [
            Node("trigger", "スケジュール", "trigger", [MainOut], WorkflowJsonContext.Default.TriggerSettings, new TriggerSettings("毎日 9:00")) with { Start = true },
            Node("http", "HTTP リクエスト", "action", [MainIn, MainOut], WorkflowJsonContext.Default.HttpSettings, new HttpSettings("GET", "https://example.com/api/orders")),
            Node(
                "branch",
                "条件分岐",
                "action",
                [
                    MainIn,
                    new DiagramPortDefinition { Id = "true", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right, Ratio = 0.3, Kind = "main", Label = "true" },
                    new DiagramPortDefinition { Id = "false", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right, Ratio = 0.7, Kind = "main", Label = "false" }
                ],
                WorkflowJsonContext.Default.BranchSettings,
                new BranchSettings("count > 0")),
            Node(
                "agent",
                "AI エージェント",
                "ai",
                [
                    MainIn,
                    MainOut,
                    new DiagramPortDefinition { Id = "model", Direction = DiagramPortDirection.Out, Side = DiagramSide.Bottom, Ratio = 0.3, Kind = "ai-model", LinkType = "ai", MaxLinks = 1, Required = true, Label = "モデル" },
                    new DiagramPortDefinition { Id = "tools", Direction = DiagramPortDirection.Out, Side = DiagramSide.Bottom, Ratio = 0.7, Kind = "ai-tool", LinkType = "ai", Label = "ツール" }
                ],
                WorkflowJsonContext.Default.AgentSettings,
                new AgentSettings("受注の内容を読んで、お客様への返信を考える")) with
            {
                Slots = [new DiagramLabelSlot { Name = "name", Placement = DiagramLabelPlacement.Above, Width = 120, MaxLines = 2 }]
            },
            Node("model", "言語モデル", "ai", [new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Top, Kind = "ai-model" }], WorkflowJsonContext.Default.ModelSettings, new ModelSettings("claude-sonnet-5-5")),
            Node("tool", "ツール", "ai", [new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Top, Kind = "ai-tool" }], WorkflowJsonContext.Default.ToolSettings, new ToolSettings("在庫を調べる")),
            Node("notify", "通知", "action", [MainIn], WorkflowJsonContext.Default.NotifySettings, new NotifySettings("#orders"))
        ],
        Categories =
        [
            new DiagramCategory { Name = "trigger", DisplayName = "トリガー" },
            new DiagramCategory { Name = "action", DisplayName = "処理" },
            new DiagramCategory { Name = "ai", DisplayName = "AI" }
        ],
        LinkTypes =
        [
            new DiagramLinkType { Name = "main", AllowCycles = false, Look = new DiagramLook { Stroke = "var(--example-flow-line)" } },
            new DiagramLinkType { Name = "ai", TargetMarker = DiagramMarker.None, Look = new DiagramLook { Stroke = "#A855F7", Dash = DiagramDash.Dashed } }
        ],
        DefaultLinkType = "main",
        IconSets =
        [
            new DiagramIconSet
            {
                Prefix = Prefix,
                Icons =
                [
                    Icon("trigger", TriggerIcon),
                    Icon("http", HttpIcon),
                    Icon("branch", BranchIcon),
                    Icon("agent", AgentIcon),
                    Icon("model", ModelIcon),
                    Icon("tool", ToolIcon),
                    Icon("notify", NotifyIcon)
                ]
            }
        ]
    };

    public static Diagram Create()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Step("start", "trigger", 40, 120, "毎朝の受注チェック", new TriggerSettings("毎日 9:00")));
        diagram.Add(Step("fetch", "http", 200, 120, "受注を取る", new HttpSettings("GET", "https://example.com/api/orders")));
        diagram.Add(Step("check", "branch", 360, 120, "新しい受注がある", new BranchSettings("count > 0")));
        diagram.Add(Step("agent", "agent", 540, 60, "返信を考える", new AgentSettings("受注の内容を読んで、お客様への返信を考える")));
        diagram.Add(Step("model", "model", 480, 230, "言語モデル", new ModelSettings("claude-sonnet-5-5")));
        diagram.Add(Step("stock", "tool", 600, 230, "在庫を調べる", new ToolSettings("在庫を調べる")));
        diagram.Add(Step("reply", "notify", 740, 60, "返信を送る", new NotifySettings("#orders")));
        diagram.Add(Step("idle", "notify", 740, 230, "担当に知らせる", new NotifySettings("#team")));
        diagram.Connect(Link("start-fetch", "start", "out", "fetch", "in", "main"));
        diagram.Connect(Link("fetch-check", "fetch", "out", "check", "in", "main"));
        diagram.Connect(Link("check-agent", "check", "true", "agent", "in", "main"));
        diagram.Connect(Link("check-idle", "check", "false", "idle", "in", "main"));
        diagram.Connect(Link("agent-reply", "agent", "out", "reply", "in", "main"));
        diagram.Connect(Link("agent-model", "agent", "model", "model", "in", "ai"));
        diagram.Connect(Link("agent-stock", "agent", "tools", "stock", "in", "ai"));
        diagram.ClearHistory();
        return diagram;
    }

    public static ImmutableArray<DiagramIssue> Check(Diagram diagram) =>
        [
            .. diagram.Validate(),
            .. diagram.Snapshot.GetUnreachable(diagram.Schema).Select(static x => new DiagramIssue(DiagramIssueSeverity.Warning, DiagramIssueKind.App, "開始から届きません") { ElementIds = [x.Id] })
        ];

    private static DiagramNodeType Node(string name, string displayName, string category, ImmutableArray<DiagramPortDefinition> ports, JsonTypeInfo dataType, object defaultData) => new()
    {
        Name = name,
        DisplayName = displayName,
        Category = category,
        Size = new DiagramSize(64, 64),
        Look = new DiagramLook { Icon = $"{Prefix}:{name}", Fill = "var(--example-node-fill)", Stroke = "var(--example-node-stroke)" },
        Slots = [new DiagramLabelSlot { Name = "name", Placement = DiagramLabelPlacement.Below, Width = 120, MaxLines = 2 }],
        Ports = ports,
        DataType = dataType,
        DefaultData = defaultData,
        CreateContent = _ => new DiagramContent { Label = displayName, Data = defaultData }
    };

    private static DiagramIcon Icon(string id, string body) =>
        new() { Id = id, Body = $"<g transform=\"translate(6 6)\">{body}</g>", Width = 36, Height = 36 };

    private static DiagramNode Step(string id, string type, double x, double y, string label, object data) =>
        new() { Id = id, Type = type, Bounds = new DiagramRect(x, y, 64, 64), Label = label, Data = data };

    private static DiagramLink Link(string id, string source, string sourcePort, string target, string targetPort, string type) =>
        new() { Id = id, Type = type, Source = new DiagramLinkEnd(source, sourcePort), Target = new DiagramLinkEnd(target, targetPort) };
}
