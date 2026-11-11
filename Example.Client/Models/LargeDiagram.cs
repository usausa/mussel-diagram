namespace Example.Client.Models;

public static class LargeDiagram
{
    private const int NodeCount = 500;

    private const int LinkCount = 600;

    private static readonly string[] Types = ["server", "database", "balancer"];

    private static readonly string[] Names = ["Amazon EC2 web server", "Amazon RDS database", "Application Load Balancer", "AWS Lambda function", "Amazon ElastiCache node"];

    public static Diagram Create()
    {
        var diagram = new Diagram(SampleDiagrams.Schema);
        diagram.Add(Group("cloud", "cloud", 20, 20, 4160, 2560, null, "AWS Cloud"));
        var subnets = new List<(string Id, double X, double Y, int Vpc)>();
        for (var r = 0; r < 2; r++)
        {
            var region = $"region{r}";
            var rx = 60 + (r * 2060);
            diagram.Add(Group(region, "vpc", rx, 80, 2040, 2470, "cloud", r == 0 ? "Asia Pacific (Tokyo)" : "Asia Pacific (Osaka)"));
            for (var v = 0; v < 2; v++)
            {
                var vpc = $"vpc{r}{v}";
                var vx = rx + 40;
                var vy = 140 + (v * 1200);
                diagram.Add(Group(vpc, "vpc", vx, vy, 1960, 1150, region, $"VPC 10.{(r * 2) + v}.0.0/16"));
                for (var a = 0; a < 2; a++)
                {
                    var zone = $"az{r}{v}{a}";
                    var ax = vx + 30 + (a * 965);
                    var ay = vy + 50;
                    diagram.Add(Group(zone, "vpc", ax, ay, 935, 1080, vpc, $"Availability Zone {(char)('a' + a)}"));
                    for (var s = 0; s < 2; s++)
                    {
                        var subnet = $"subnet{r}{v}{a}{s}";
                        var sx = ax + 25;
                        var sy = ay + 45 + (s * 510);
                        diagram.Add(Group(subnet, "subnet", sx, sy, 885, 490, zone, s == 0 ? "Public subnet" : "Private subnet"));
                        subnets.Add((subnet, sx, sy, (r * 2) + v));
                    }
                }
            }
        }

        var services = new Dictionary<(int Subnet, int Column), int>();
        for (var k = 0; k < 19; k++)
        {
            var subnet = 1 + (k % 15);
            var column = (k / 15) * 4;
            services[(subnet, column)] = k;
            var (id, x, y, _) = subnets[subnet];
            diagram.Add(Group($"svc{k}", "subnet", x + 18 + (column * 105), y + 28, 200, 222, id, $"Service {k + 1}"));
        }

        var nodes = new List<(string Id, int Vpc)>();
        for (var i = 0; (i < subnets.Count) && (nodes.Count < NodeCount); i++)
        {
            var (subnetId, x, y, vpc) = subnets[i];
            for (var row = 0; (row < 4) && (nodes.Count < NodeCount); row++)
            {
                for (var column = 0; (column < 8) && (nodes.Count < NodeCount); column++)
                {
                    var id = $"n{nodes.Count:000}";
                    var parentId = (row < 2) && services.TryGetValue((i, column - (column % 2)), out var k) ? $"svc{k}" : subnetId;
                    diagram.Add(new DiagramNode
                    {
                        Id = id,
                        Type = Types[nodes.Count % Types.Length],
                        Bounds = new DiagramRect(x + 40 + (column * 105), y + 45 + (row * 115), 48, 48),
                        ParentId = parentId,
                        Label = $"{Names[nodes.Count % Names.Length]} {nodes.Count:000}"
                    });
                    nodes.Add((id, vpc));
                }
            }
        }

        var byVpc = nodes.GroupBy(static x => x.Vpc).ToDictionary(static x => x.Key, static x => x.Select(static n => n.Id).ToArray());
        var all = nodes.Select(static x => x.Id).ToArray();
        for (var k = 0; k < LinkCount; k++)
        {
            var (source, vpc) = nodes[(k * 37) % nodes.Count];
            var pool = (k % 7) == 0 ? all : byVpc[vpc];
            var index = ((k * 53) + 11) % pool.Length;
            var target = pool[index] == source ? pool[(index + 1) % pool.Length] : pool[index];
            diagram.Connect(new DiagramLink { Id = $"l{k:000}", Type = "arrow", Source = new DiagramLinkEnd(source), Target = new DiagramLinkEnd(target) });
        }

        return diagram;
    }

    private static DiagramGroup Group(string id, string type, double x, double y, double width, double height, string? parentId, string label) =>
        new() { Id = id, Type = type, Bounds = new DiagramRect(x, y, width, height), ParentId = parentId, Label = label };
}
