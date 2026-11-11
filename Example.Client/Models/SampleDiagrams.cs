namespace Example.Client.Models;

public static class SampleDiagrams
{
    private const string ServerIcon = "<rect x=\"3\" y=\"4\" width=\"18\" height=\"7\" rx=\"1\" fill=\"#ED7100\" /><rect x=\"3\" y=\"13\" width=\"18\" height=\"7\" rx=\"1\" fill=\"#ED7100\" /><circle cx=\"7\" cy=\"7.5\" r=\"1.2\" fill=\"#fff\" /><circle cx=\"7\" cy=\"16.5\" r=\"1.2\" fill=\"#fff\" />";

    private const string DatabaseIcon = "<ellipse cx=\"12\" cy=\"6\" rx=\"8\" ry=\"3\" fill=\"#C925D1\" /><path d=\"M4 6v12c0 1.7 3.6 3 8 3s8-1.3 8-3V6c0 1.7-3.6 3-8 3S4 7.7 4 6z\" fill=\"#C925D1\" />";

    private const string BalancerIcon = "<circle cx=\"12\" cy=\"12\" r=\"9\" fill=\"#8C4FFF\" /><path d=\"M7 12h10M12 7l5 5-5 5\" stroke=\"#fff\" stroke-width=\"2\" fill=\"none\" />";

    private const string CloudIcon = "<path d=\"M7 18h10a4 4 0 0 0 0-8 5 5 0 0 0-9.6-1.5A3.5 3.5 0 0 0 7 18z\" fill=\"#232F3E\" />";

    public static DiagramSchema Schema { get; } = new()
    {
        NodeTypes =
        [
            IconNode("server", "Server", "sample:server"),
            IconNode("database", "Database", "sample:database"),
            IconNode("balancer", "Load balancer", "sample:balancer"),
            new DiagramNodeType
            {
                Name = "process",
                DisplayName = "Process",
                Size = new DiagramSize(140, 60),
                Look = new DiagramLook { Fill = "#E3F2FD", Stroke = "#1E88E5" },
                Ports =
                [
                    new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left },
                    new DiagramPortDefinition { Id = "out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right }
                ]
            },
            new DiagramNodeType
            {
                Name = "decision",
                DisplayName = "Decision",
                Size = new DiagramSize(120, 80),
                Look = new DiagramLook { Shape = DiagramShape.Diamond, Fill = "#FFF3E0", Stroke = "#FB8C00" }
            }
        ],
        GroupTypes =
        [
            new DiagramGroupType { Name = "cloud", DisplayName = "Cloud", Look = new DiagramLook { Icon = "sample:cloud", Stroke = "#232F3E" } },
            new DiagramGroupType { Name = "vpc", DisplayName = "VPC", Look = new DiagramLook { Stroke = "#8C4FFF" } },
            new DiagramGroupType { Name = "subnet", DisplayName = "Subnet", Look = new DiagramLook { Stroke = "#7AA116", Fill = "rgba(122, 161, 22, 0.06)", Dash = DiagramDash.Dashed } }
        ],
        LinkTypes = [new DiagramLinkType { Name = "arrow", DisplayName = "Arrow" }],
        IconSets =
        [
            new DiagramIconSet
            {
                Prefix = "sample",
                Icons =
                [
                    new DiagramIcon { Id = "server", Body = ServerIcon },
                    new DiagramIcon { Id = "database", Body = DatabaseIcon },
                    new DiagramIcon { Id = "balancer", Body = BalancerIcon },
                    new DiagramIcon { Id = "cloud", Body = CloudIcon }
                ]
            }
        ],
        DefaultLinkType = "arrow"
    };

    public static Diagram CreateBasic()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramGroup { Id = "cloud", Type = "cloud", Bounds = new DiagramRect(20, 20, 760, 420), Label = "AWS Cloud" });
        diagram.Add(new DiagramGroup { Id = "vpc", Type = "vpc", Bounds = new DiagramRect(160, 70, 600, 350), ParentId = "cloud", Label = "VPC 10.0.0.0/16" });
        diagram.Add(new DiagramGroup { Id = "public", Type = "subnet", Bounds = new DiagramRect(180, 110, 260, 290), ParentId = "vpc", Label = "Public subnet" });
        diagram.Add(new DiagramGroup { Id = "private", Type = "subnet", Bounds = new DiagramRect(470, 110, 270, 290), ParentId = "vpc", Label = "Private subnet" });
        diagram.Add(Icon("lb", "balancer", 50, 160, "cloud", "Application Load Balancer"));
        diagram.Add(Icon("web1", "server", 230, 150, "public", "Web server 1"));
        diagram.Add(Icon("web2", "server", 230, 280, "public", "Web server 2"));
        diagram.Add(Icon("db", "database", 580, 210, "private", "Amazon RDS primary database"));
        diagram.Add(new DiagramNode { Id = "job", Type = "process", Bounds = new DiagramRect(330, 160, 90, 50), ParentId = "public", Label = "Nightly batch" });
        diagram.Add(new DiagramNode { Id = "check", Type = "decision", Bounds = new DiagramRect(500, 300, 100, 70), ParentId = "private", Label = "OK?" });
        diagram.Connect(Link("lb-web1", "lb", "web1"));
        diagram.Connect(Link("lb-web2", "lb", "web2"));
        diagram.Connect(Link("web1-db", "web1", "db"));
        diagram.Connect(Link("web2-db", "web2", "db"));
        diagram.Connect(Link("job-check", "job", "check"));
        return diagram;
    }

    private static DiagramNodeType IconNode(string name, string displayName, string icon) => new()
    {
        Name = name,
        DisplayName = displayName,
        Size = new DiagramSize(48, 48),
        Look = new DiagramLook { Icon = icon, Fill = "none", Stroke = "none" },
        Slots = [new DiagramLabelSlot { Name = "name", Placement = DiagramLabelPlacement.Below, Width = 110, MaxLines = 2 }]
    };

    private static DiagramNode Icon(string id, string type, double x, double y, string parentId, string label) =>
        new() { Id = id, Type = type, Bounds = new DiagramRect(x, y, 48, 48), ParentId = parentId, Label = label };

    private static DiagramLink Link(string id, string source, string target) =>
        new() { Id = id, Type = "arrow", Source = new DiagramLinkEnd(source), Target = new DiagramLinkEnd(target) };
}
