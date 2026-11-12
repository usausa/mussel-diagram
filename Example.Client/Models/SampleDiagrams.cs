namespace Example.Client.Models;

using System.Collections.Immutable;

public static class SampleDiagrams
{
    private const string StarIcon = "<path d=\"M12 2.5l2.9 6 6.6.9-4.8 4.6 1.2 6.5L12 17.4l-5.9 3.1 1.2-6.5-4.8-4.6 6.6-.9z\" fill=\"#F59E0B\" />";

    private const string TriangleIcon = "<path d=\"M12 3l10 18H2z\" fill=\"#10B981\" />";

    private const string PinIcon = "<path d=\"M12 2a7 7 0 0 0-7 7c0 5.2 7 13 7 13s7-7.8 7-13a7 7 0 0 0-7-7z\" fill=\"#EF4444\" /><circle cx=\"12\" cy=\"9\" r=\"2.6\" fill=\"#FFFFFF\" />";

    private const string FrameIcon = "<rect x=\"3\" y=\"3\" width=\"8\" height=\"8\" rx=\"1\" fill=\"currentColor\" /><rect x=\"13\" y=\"3\" width=\"8\" height=\"8\" rx=\"1\" fill=\"currentColor\" /><rect x=\"3\" y=\"13\" width=\"8\" height=\"8\" rx=\"1\" fill=\"currentColor\" /><rect x=\"13\" y=\"13\" width=\"8\" height=\"8\" rx=\"1\" fill=\"currentColor\" />";

    private const string Hexagon = "M0.25,0L0.75,0L1,0.5L0.75,1L0.25,1L0,0.5Z";

    public static DiagramSchema Schema { get; } = new()
    {
        NodeTypes =
        [
            new DiagramNodeType
            {
                Name = "rectangle",
                DisplayName = "四角",
                Category = "shape",
                Size = new DiagramSize(140, 60),
                Look = new DiagramLook { Fill = "var(--example-blue-fill)", Stroke = "var(--example-blue-stroke)" },
                Ports =
                [
                    new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left },
                    new DiagramPortDefinition { Id = "out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right }
                ]
            },
            new DiagramNodeType
            {
                Name = "ellipse",
                DisplayName = "楕円",
                Category = "shape",
                Size = new DiagramSize(120, 60),
                Look = new DiagramLook { Shape = DiagramShape.Ellipse, Fill = "var(--example-purple-fill)", Stroke = "var(--example-purple-stroke)" }
            },
            new DiagramNodeType
            {
                Name = "diamond",
                DisplayName = "ひし形",
                Category = "shape",
                Size = new DiagramSize(120, 80),
                Look = new DiagramLook { Shape = DiagramShape.Diamond, Fill = "var(--example-orange-fill)", Stroke = "var(--example-orange-stroke)" }
            },
            new DiagramNodeType
            {
                Name = "hexagon",
                DisplayName = "六角形",
                Category = "shape",
                Size = new DiagramSize(120, 80),
                Look = new DiagramLook { Shape = DiagramShape.Path, Path = Hexagon, Fill = "var(--example-teal-fill)", Stroke = "var(--example-teal-stroke)" }
            },
            new DiagramNodeType
            {
                Name = "card",
                DisplayName = "カード",
                Category = "shape",
                Size = new DiagramSize(160, 60),
                Look = new DiagramLook { Fill = "var(--example-green-fill)", Stroke = "var(--example-green-stroke)" },
                Slots = [new DiagramLabelSlot { Name = "title" }, new DiagramLabelSlot { Name = "note", DefaultText = "補足" }],
                Ports =
                [
                    new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left },
                    new DiagramPortDefinition { Id = "out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right, Required = true }
                ],
                CreateContent = static c => new DiagramContent { Label = c.Value ?? "新しいカード" }
            },
            IconNode("star", "星", "sample:star") with
            {
                CreateContent = static c => new DiagramContent { Label = c.Value ?? $"星 {c.Snapshot.Nodes.Count(x => x.Type == "star") + 1}" }
            },
            IconNode("triangle", "三角", "sample:triangle") with { GetTooltip = static box => box.Label is null ? null : $"三角: {box.Label}" },
            IconNode("pin", "ピン", "sample:pin")
        ],
        GroupTypes =
        [
            new DiagramGroupType { Name = "frame", DisplayName = "枠", Category = "group", Look = new DiagramLook { Icon = "sample:frame", Stroke = "var(--example-node-stroke)" } },
            new DiagramGroupType { Name = "group", DisplayName = "グループ", Category = "group", Look = new DiagramLook { Stroke = "#6366F1" } },
            new DiagramGroupType { Name = "dashed", DisplayName = "点線のグループ", Category = "group", Look = new DiagramLook { Stroke = "#16A34A", Fill = "rgba(22, 163, 74, 0.06)", Dash = DiagramDash.Dashed } }
        ],
        Categories =
        [
            new DiagramCategory { Name = "shape", DisplayName = "図形" },
            new DiagramCategory { Name = "icon", DisplayName = "アイコン" },
            new DiagramCategory { Name = "group", DisplayName = "グループ" }
        ],
        DefaultGroupType = "group",
        LinkTypes =
        [
            new DiagramLinkType { Name = "arrow", DisplayName = "矢印" },
            new DiagramLinkType { Name = "line", DisplayName = "線", TargetMarker = DiagramMarker.None },
            new DiagramLinkType { Name = "two-way", DisplayName = "両矢印", SourceMarker = DiagramMarker.Arrow },
            new DiagramLinkType { Name = "dashed", DisplayName = "点線", Look = new DiagramLook { Dash = DiagramDash.Dashed } },
            new DiagramLinkType { Name = "straight", DisplayName = "直線", Form = DiagramLineForm.Straight },
            new DiagramLinkType { Name = "curve", DisplayName = "曲線", Form = DiagramLineForm.Curved }
        ],
        DefaultLinkType = "arrow",
        IconSets =
        [
            new DiagramIconSet
            {
                Prefix = "sample",
                Icons =
                [
                    new DiagramIcon { Id = "star", Body = StarIcon },
                    new DiagramIcon { Id = "triangle", Body = TriangleIcon },
                    new DiagramIcon { Id = "pin", Body = PinIcon },
                    new DiagramIcon { Id = "frame", Body = FrameIcon }
                ]
            }
        ]
    };

    public static Diagram CreateBasic()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Group("frame", "frame", 20, 20, 760, 420, null, "枠"));
        diagram.Add(Group("group", "group", 160, 60, 600, 360, "frame", "グループ"));
        diagram.Add(Group("left", "dashed", 180, 100, 270, 300, "group", "点線のグループ 1"));
        diagram.Add(Group("right", "dashed", 470, 100, 270, 300, "group", "点線のグループ 2"));
        diagram.Add(Icon("pin", "pin", 46, 156, "frame", "ピン"));
        diagram.Add(Icon("star1", "star", 226, 146, "left", "星 1"));
        diagram.Add(Icon("star2", "star", 226, 276, "left", "星 2"));
        diagram.Add(Icon("triangle", "triangle", 576, 206, "right", "長いラベルは 2 行まで折り返します"));
        diagram.Add(new DiagramNode { Id = "rect", Type = "rectangle", Bounds = new DiagramRect(330, 160, 100, 60), ParentId = "left", Label = "四角" });
        diagram.Add(new DiagramNode { Id = "diamond", Type = "diamond", Bounds = new DiagramRect(500, 300, 100, 80), ParentId = "right", Label = "ひし形" });
        diagram.Add(new DiagramNode { Id = "ellipse", Type = "ellipse", Bounds = new DiagramRect(820, 60, 120, 60), Label = "楕円" });
        diagram.Add(new DiagramNode { Id = "hexagon", Type = "hexagon", Bounds = new DiagramRect(940, 250, 120, 80), Label = "六角形" });
        diagram.Add(new DiagramNode
        {
            Id = "card",
            Type = "card",
            Bounds = new DiagramRect(820, 400, 160, 60),
            Label = "カード",
            Texts = ImmutableDictionary<string, string>.Empty.Add("note", "テンプレートで描く")
        });
        diagram.Connect(Link("pin-star1", "arrow", "pin", "star1"));
        diagram.Connect(Link("pin-star2", "line", "pin", "star2"));
        diagram.Connect(Link("star1-triangle", "arrow", "star1", "triangle"));
        diagram.Connect(Link("star2-triangle", "two-way", "star2", "triangle"));
        diagram.Connect(Link("rect-diamond", "dashed", "rect", "diamond"));
        diagram.Connect(Link("ellipse-hexagon", "curve", "ellipse", "hexagon"));
        diagram.Connect(Link("hexagon-card", "straight", "hexagon", "card"));
        diagram.ClearHistory();
        return diagram;
    }

    private static DiagramNodeType IconNode(string name, string displayName, string icon) => new()
    {
        Name = name,
        DisplayName = displayName,
        Category = "icon",
        Size = new DiagramSize(48, 48),
        Look = new DiagramLook { Icon = icon, Fill = "none", Stroke = "none" },
        Slots = [new DiagramLabelSlot { Name = "name", Placement = DiagramLabelPlacement.Below, Width = 110, MaxLines = 2 }]
    };

    private static DiagramGroup Group(string id, string type, double x, double y, double width, double height, string? parentId, string label) =>
        new() { Id = id, Type = type, Bounds = new DiagramRect(x, y, width, height), ParentId = parentId, Label = label };

    private static DiagramNode Icon(string id, string type, double x, double y, string parentId, string label) =>
        new() { Id = id, Type = type, Bounds = new DiagramRect(x, y, 48, 48), ParentId = parentId, Label = label };

    private static DiagramLink Link(string id, string type, string source, string target) =>
        new() { Id = id, Type = type, Source = new DiagramLinkEnd(source), Target = new DiagramLinkEnd(target) };
}
