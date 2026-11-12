namespace Example.Client.Models;

public static class LargeDiagram
{
    private const int NodeCount = 500;

    private const int LinkCount = 600;

    private const string Prefix = "net";

    private const string NetworkIcon = "<circle cx=\"12\" cy=\"5\" r=\"2.5\" fill=\"currentColor\" /><circle cx=\"5\" cy=\"18\" r=\"2.5\" fill=\"currentColor\" /><circle cx=\"19\" cy=\"18\" r=\"2.5\" fill=\"currentColor\" /><path d=\"M12 7.5v4M12 11.5 6.5 16M12 11.5l5.5 4.5\" stroke=\"currentColor\" stroke-width=\"1.6\" fill=\"none\" />";

    private static readonly Device[] Devices =
    [
        new("pc", "PC", "<rect x=\"3\" y=\"4\" width=\"18\" height=\"12\" rx=\"1.5\" fill=\"#0EA5E9\" /><rect x=\"5\" y=\"6\" width=\"14\" height=\"8\" fill=\"#FFFFFF\" opacity=\"0.85\" /><path d=\"M9 20h6M12 16v4\" stroke=\"#0EA5E9\" stroke-width=\"2\" stroke-linecap=\"round\" />"),
        new("server", "サーバー", "<rect x=\"4\" y=\"3\" width=\"16\" height=\"8\" rx=\"1.5\" fill=\"#F97316\" /><rect x=\"4\" y=\"13\" width=\"16\" height=\"8\" rx=\"1.5\" fill=\"#F97316\" /><circle cx=\"8\" cy=\"7\" r=\"1.2\" fill=\"#FFFFFF\" /><circle cx=\"8\" cy=\"17\" r=\"1.2\" fill=\"#FFFFFF\" />"),
        new("switch", "スイッチ", "<rect x=\"2\" y=\"8\" width=\"20\" height=\"8\" rx=\"1.5\" fill=\"#6366F1\" /><path d=\"M5 12h2M9 12h2M13 12h2M17 12h2\" stroke=\"#FFFFFF\" stroke-width=\"2\" />"),
        new("printer", "プリンター", "<rect x=\"6\" y=\"3\" width=\"12\" height=\"6\" fill=\"#64748B\" /><rect x=\"3\" y=\"9\" width=\"18\" height=\"8\" rx=\"1.5\" fill=\"#64748B\" /><rect x=\"7\" y=\"14\" width=\"10\" height=\"7\" fill=\"#FFFFFF\" stroke=\"#64748B\" stroke-width=\"1.5\" />"),
        new("access-point", "無線 AP", "<circle cx=\"12\" cy=\"18\" r=\"2\" fill=\"#10B981\" /><path d=\"M8 14.5a5.5 5.5 0 0 1 8 0M5 11.5a9.5 9.5 0 0 1 14 0\" stroke=\"#10B981\" stroke-width=\"2\" fill=\"none\" stroke-linecap=\"round\" />")
    ];

    private static readonly string[] Areas = ["東日本", "西日本"];

    private static readonly string[][] Sites = [["東京本社", "仙台支社"], ["大阪支社", "福岡支社"]];

    private static readonly string[] Sections = ["営業部", "総務部", "経理部", "人事部", "開発部", "企画部", "情報システム部", "品質保証部", "購買部", "広報部"];

    public static DiagramSchema Schema { get; } = new()
    {
        NodeTypes =
        [
            .. Devices.Select(static x => new DiagramNodeType
            {
                Name = x.Name,
                DisplayName = x.DisplayName,
                Size = new DiagramSize(48, 48),
                Look = new DiagramLook { Icon = $"{Prefix}:{x.Name}", Fill = "none", Stroke = "none" },
                Slots = [new DiagramLabelSlot { Name = "name", Placement = DiagramLabelPlacement.Below, Width = 110, MaxLines = 2 }]
            })
        ],
        GroupTypes =
        [
            new DiagramGroupType { Name = "network", DisplayName = "社内ネットワーク", Look = new DiagramLook { Icon = $"{Prefix}:network", Stroke = "var(--example-node-stroke)" } },
            new DiagramGroupType { Name = "area", DisplayName = "地域", Look = new DiagramLook { Stroke = "#0EA5E9" } },
            new DiagramGroupType { Name = "site", DisplayName = "拠点", Look = new DiagramLook { Stroke = "#6366F1" } },
            new DiagramGroupType { Name = "building", DisplayName = "建屋", Look = new DiagramLook { Stroke = "#64748B", Dash = DiagramDash.Dashed } },
            new DiagramGroupType { Name = "floor", DisplayName = "フロア", Look = new DiagramLook { Stroke = "#16A34A", Fill = "rgba(22, 163, 74, 0.06)", Dash = DiagramDash.Dashed } },
            new DiagramGroupType { Name = "section", DisplayName = "部署", Look = new DiagramLook { Stroke = "#F59E0B", Fill = "rgba(245, 158, 11, 0.06)" } }
        ],
        LinkTypes = [new DiagramLinkType { Name = "cable", DisplayName = "配線", TargetMarker = DiagramMarker.None }],
        DefaultLinkType = "cable",
        IconSets =
        [
            new DiagramIconSet
            {
                Prefix = Prefix,
                Icons = [new DiagramIcon { Id = "network", Body = NetworkIcon }, .. Devices.Select(static x => new DiagramIcon { Id = x.Name, Body = x.Icon })]
            }
        ]
    };

    public static Diagram Create()
    {
        var diagram = new Diagram(Schema);
        using (diagram.BeginUpdate(new DiagramWriteOptions { History = DiagramHistoryMode.Skip }))
        {
            Build(diagram);
        }

        return diagram;
    }

    private static void Build(Diagram diagram)
    {
        diagram.Add(Group("network", "network", 20, 20, 4160, 2560, null, "社内ネットワーク"));
        var floors = new List<(string Id, double X, double Y, int Site)>();
        for (var r = 0; r < 2; r++)
        {
            var area = $"area{r}";
            var rx = 60 + (r * 2060);
            diagram.Add(Group(area, "area", rx, 80, 2040, 2470, "network", Areas[r]));
            for (var v = 0; v < 2; v++)
            {
                var site = $"site{r}{v}";
                var vx = rx + 40;
                var vy = 140 + (v * 1200);
                diagram.Add(Group(site, "site", vx, vy, 1960, 1150, area, Sites[r][v]));
                for (var a = 0; a < 2; a++)
                {
                    var building = $"building{r}{v}{a}";
                    var ax = vx + 30 + (a * 960);
                    var ay = vy + 50;
                    diagram.Add(Group(building, "building", ax, ay, 940, 1080, site, $"{(char)('A' + a)} 棟"));
                    for (var s = 0; s < 2; s++)
                    {
                        var floor = $"floor{r}{v}{a}{s}";
                        var sx = ax + 20;
                        var sy = ay + 40 + (s * 510);
                        diagram.Add(Group(floor, "floor", sx, sy, 890, 500, building, $"{s + 1}F"));
                        floors.Add((floor, sx, sy, (r * 2) + v));
                    }
                }
            }
        }

        var sections = new Dictionary<(int Floor, int Column), int>();
        for (var k = 0; k < 19; k++)
        {
            var floor = 1 + (k % 15);
            var column = (k / 15) * 4;
            sections[(floor, column)] = k;
            var (id, x, y, _) = floors[floor];
            diagram.Add(Group($"section{k}", "section", x + 20 + (column * 110), y + 40, 210, 240, id, Sections[k % Sections.Length]));
        }

        var nodes = new List<(string Id, int Site)>();
        for (var i = 0; (i < floors.Count) && (nodes.Count < NodeCount); i++)
        {
            var (floorId, x, y, site) = floors[i];
            for (var row = 0; (row < 4) && (nodes.Count < NodeCount); row++)
            {
                for (var column = 0; (column < 8) && (nodes.Count < NodeCount); column++)
                {
                    var id = $"n{nodes.Count:000}";
                    var parentId = (row < 2) && sections.TryGetValue((i, column - (column % 2)), out var k) ? $"section{k}" : floorId;
                    var device = Devices[nodes.Count % Devices.Length];
                    diagram.Add(new DiagramNode
                    {
                        Id = id,
                        Type = device.Name,
                        Bounds = new DiagramRect(x + 46 + (column * 110), y + 86 + (row * 110), 48, 48),
                        ParentId = parentId,
                        Label = $"{device.DisplayName} {nodes.Count:000}"
                    });
                    nodes.Add((id, site));
                }
            }
        }

        var bySite = nodes.GroupBy(static x => x.Site).ToDictionary(static x => x.Key, static x => x.Select(static n => n.Id).ToArray());
        var all = nodes.Select(static x => x.Id).ToArray();
        for (var k = 0; k < LinkCount; k++)
        {
            var (source, site) = nodes[(k * 37) % nodes.Count];
            var pool = (k % 7) == 0 ? all : bySite[site];
            var index = ((k * 53) + 11) % pool.Length;
            var target = pool[index] == source ? pool[(index + 1) % pool.Length] : pool[index];
            diagram.Connect(new DiagramLink { Id = $"l{k:000}", Type = "cable", Source = new DiagramLinkEnd(source), Target = new DiagramLinkEnd(target) });
        }
    }

    private static DiagramGroup Group(string id, string type, double x, double y, double width, double height, string? parentId, string label) =>
        new() { Id = id, Type = type, Bounds = new DiagramRect(x, y, width, height), ParentId = parentId, Label = label };

    private sealed record Device(string Name, string DisplayName, string Icon);
}
