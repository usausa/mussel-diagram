namespace Example.Client.Models;

using System.Collections.Immutable;

public static class AwsDiagrams
{
    public const string Prefix = "aws";

    private const double LineWidth = 1.67;

    private const string GroupCategory = "aws-groups";

    private static readonly DiagramTextFormat GroupName = new() { FontFamily = "Arial, sans-serif", FontSize = 16, Color = "var(--example-aws-text)" };

    private static readonly DiagramTextFormat IconName = new() { FontFamily = "Arial, sans-serif", FontSize = 16, Color = "var(--example-aws-text)" };

    private static readonly DiagramGroupHeader IconHeader = new() { IconSize = 40, NameLeft = 52.8, NameTop = 9.6 };

    private static readonly DiagramGroupHeader CenterHeader = new() { Form = DiagramHeaderForm.TopCenter, IconSize = 40 };

    private static readonly DiagramGroupHeader CenterName = new() { Form = DiagramHeaderForm.NameOnly, NameAlignment = DiagramTextAlignment.Center, NameTop = 9.6 };

    private static readonly DiagramGroupHeader LeftName = new() { Form = DiagramHeaderForm.NameOnly, NameLeft = 9.6, NameTop = 9.6 };

    private static readonly GroupStyle[] Groups =
    [
        new("cloud", "AWS Cloud", "var(--example-aws-cloud)", DiagramDash.Solid, IconHeader, ["AWS-Cloud-logo"]),
        new("cloud-plain", "AWS Cloud (no logo)", "var(--example-aws-cloud)", DiagramDash.Solid, IconHeader, ["AWS-Cloud"]),
        new("region", "Region", "#00A4A6", DiagramDash.Dotted, IconHeader, ["Region"]),
        new("availability-zone", "Availability Zone", "#00A4A6", DiagramDash.Dashed, CenterName, []),
        new("security-group", "Security group", "#DD344C", DiagramDash.Solid, LeftName, []),
        new("auto-scaling-group", "Auto Scaling group", "#ED7100", DiagramDash.Dashed, CenterHeader, ["Auto-Scaling-group"]),
        new("vpc", "Virtual private cloud (VPC)", "#8C4FFF", DiagramDash.Solid, IconHeader, ["Virtual-private-cloud-VPC", "VPC"]),
        new("private-subnet", "Private subnet", "#00A4A6", DiagramDash.Solid, IconHeader, ["Private-subnet"]),
        new("public-subnet", "Public subnet", "#7AA116", DiagramDash.Solid, IconHeader, ["Public-subnet"]),
        new("server-contents", "Server contents", "#7D8998", DiagramDash.Solid, IconHeader, ["Server-contents"]),
        new("corporate-data-center", "Corporate data center", "#7D8998", DiagramDash.Solid, IconHeader, ["Corporate-data-center"]),
        new("ec2-instance-contents", "EC2 instance contents", "#ED7100", DiagramDash.Solid, IconHeader, ["EC2-instance-contents"]),
        new("spot-fleet", "Spot Fleet", "#ED7100", DiagramDash.Solid, IconHeader, ["Spot-Fleet"]),
        new("elastic-beanstalk-container", "Elastic Beanstalk container", "#ED7100", DiagramDash.Solid, IconHeader, []),
        new("account", "AWS account", "#E7157B", DiagramDash.Solid, IconHeader, ["AWS-Account"]),
        new("step-functions-workflow", "AWS Step Functions workflow", "#E7157B", DiagramDash.Solid, IconHeader, []),
        new("iot-greengrass-deployment", "AWS IoT Greengrass Deployment", "#7AA116", DiagramDash.Solid, IconHeader, ["AWS-IoT-Greengrass-Deployment"]),
        new("iot-greengrass", "AWS IoT Greengrass", "#7AA116", DiagramDash.Solid, IconHeader, []),
        new("generic-dashed", "Generic group (dashed)", "#7D8998", DiagramDash.Dashed, CenterName, []),
        new("generic", "Generic group", "#7D8998", DiagramDash.Solid, CenterName, []),
        new("custom-analytics", "Custom group (Analytics)", "#8C4FFF", DiagramDash.Solid, IconHeader, []),
        new("custom-integration", "Custom group (Application Integration)", "#E7157B", DiagramDash.Solid, IconHeader, []),
        new("custom-ai", "Custom group (Artificial Intelligence)", "#01A88D", DiagramDash.Solid, IconHeader, []),
        new("custom-compute", "Custom group (Compute)", "#ED7100", DiagramDash.Solid, IconHeader, []),
        new("custom-security", "Custom group (Security)", "#DD344C", DiagramDash.Solid, IconHeader, []),
        new("custom-storage", "Custom group (Storage)", "#7AA116", DiagramDash.Solid, IconHeader, []),
        new("custom-database", "Custom group (Databases)", "#C925D1", DiagramDash.Solid, IconHeader, [])
    ];

    private static readonly ImmutableArray<string> InCloud = ["aws-cloud", "aws-cloud-plain", "aws-account"];

    private static readonly Dictionary<string, ImmutableArray<string>> Parents = new(StringComparer.Ordinal)
    {
        ["region"] = InCloud,
        ["vpc"] = ["aws-region", .. InCloud],
        ["availability-zone"] = ["aws-vpc", "aws-region"],
        ["public-subnet"] = ["aws-vpc", "aws-availability-zone"],
        ["private-subnet"] = ["aws-vpc", "aws-availability-zone"],
        ["security-group"] = ["aws-vpc", "aws-availability-zone", "aws-public-subnet", "aws-private-subnet"]
    };

    public static DiagramIconInfo? Map(string path)
    {
        var segments = path.Split('/');
        var file = segments[^1];
        if (!file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || file.StartsWith("._", StringComparison.Ordinal) ||
            segments.Any(static x => x.Equals("__MACOSX", StringComparison.Ordinal) || x.EndsWith("_Dark", StringComparison.Ordinal)))
        {
            return null;
        }

        var stem = file[..^4];
        if (stem.EndsWith("_Dark", StringComparison.Ordinal) || stem.StartsWith("Arch-Category_", StringComparison.Ordinal))
        {
            return null;
        }

        if (stem.EndsWith("_Light", StringComparison.Ordinal))
        {
            stem = stem[..^6];
        }

        if (stem.StartsWith("Arch_", StringComparison.Ordinal) && stem.EndsWith("_64", StringComparison.Ordinal))
        {
            var id = stem[..^3];
            return new DiagramIconInfo(id, Words(id[5..]), FolderCategory(segments, "Arch_"));
        }

        if (stem.StartsWith("Res_", StringComparison.Ordinal) && stem.EndsWith("_48", StringComparison.Ordinal))
        {
            var id = stem[..^3];
            return new DiagramIconInfo(id, Words(id[4..]), FolderCategory(segments, "Res_"));
        }

        if (stem.EndsWith("_32", StringComparison.Ordinal) && segments.Any(static x => x.Contains("Group-Icons", StringComparison.Ordinal)))
        {
            var id = stem[..^3];
            return new DiagramIconInfo(id, Words(id), GroupCategory);
        }

        return null;
    }

    public static DiagramSchema CreateSchema(DiagramIconSet? icons)
    {
        var line = new DiagramLook { Preset = "aws-line" };
        var service = new DiagramNodeType
        {
            Name = "aws-service",
            Size = new DiagramSize(80, 80),
            Look = new DiagramLook { Fill = "none", Stroke = "none" },
            Slots = [new DiagramLabelSlot { Name = "name", Placement = DiagramLabelPlacement.Below, Width = 140, MaxLines = 2, Format = IconName }],
            Permissions = DiagramPermissions.All & ~DiagramPermissions.Resize,
            ConnectsAsWhole = true
        };
        var resource = service with { Name = "aws-resource", Size = new DiagramSize(48, 48) };
        var nodes = icons is null
            ? []
            : DiagramIconImport.CreateNodeTypes(icons, service, static x => x.Id.StartsWith("Arch_", StringComparison.Ordinal))
                .AddRange(DiagramIconImport.CreateNodeTypes(icons, resource, static x => x.Id.StartsWith("Res_", StringComparison.Ordinal)));
        var groups = Groups.Select(x => new DiagramGroupType
        {
            Name = $"aws-{x.Name}",
            DisplayName = x.DisplayName,
            Category = GroupCategory,
            Size = new DiagramSize(360, 240),
            Look = new DiagramLook { Preset = $"aws-{x.Name}" },
            Header = x.Header,
            Slots =
            [
                new DiagramLabelSlot { Name = "name", Placement = DiagramLabelPlacement.Header, MaxLines = 1, Format = GroupName },
                new DiagramLabelSlot { Name = "note", Placement = DiagramLabelPlacement.Bottom, MaxLines = 1, Format = GroupName, Editable = true }
            ],
            Padding = new DiagramThickness(20, 50, 20, 30),
            AllowedParents = Parents.TryGetValue(x.Name, out var parents) ? parents : null
        }).ToList();
        groups.Add(groups.Single(static x => x.Name == "aws-availability-zone") with { Name = "aws-availability-zone-overlay", DisplayName = "Availability Zone (across)", Kind = DiagramGroupKind.Overlay, AllowedParents = null });
        var scaling = groups.FindIndex(static x => x.Name == "aws-auto-scaling-group");
        groups[scaling] = groups[scaling] with { Kind = DiagramGroupKind.Overlay };
        return new DiagramSchema
        {
            NodeTypes = nodes,
            GroupTypes = [.. groups],
            LinkTypes =
            [
                new DiagramLinkType { Name = "aws-arrow", DisplayName = "Arrow", Look = line, TargetMarker = DiagramMarker.OpenArrow },
                new DiagramLinkType { Name = "aws-two-way", DisplayName = "Two-way arrow", Look = line, SourceMarker = DiagramMarker.OpenArrow, TargetMarker = DiagramMarker.OpenArrow },
                new DiagramLinkType { Name = "aws-line", DisplayName = "Line", Look = line }
            ],
            DefaultLinkType = "aws-arrow",
            Presets =
            [
                .. Groups.Select(x => new DiagramPreset
                {
                    Name = $"aws-{x.Name}",
                    Look = new DiagramLook { Fill = "none", Stroke = x.Color, StrokeWidth = LineWidth, Dash = x.Dash, Icon = FindIcon(icons, x.Icons) }
                }),
                new DiagramPreset { Name = "aws-line", Look = new DiagramLook { Stroke = "var(--example-aws-arrow)", StrokeWidth = LineWidth } }
            ],
            Categories =
            [
                new DiagramCategory { Name = GroupCategory, DisplayName = "Groups" },
                .. nodes.Select(static x => x.Category).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(static x => new DiagramCategory { Name = x, DisplayName = x, Collapsed = true })
            ],
            IconSets = icons is null ? [] : [icons]
        };
    }

    public static Diagram CreateSample(DiagramSchema schema)
    {
        var diagram = new Diagram(schema);
        diagram.Add(Group("cloud", "aws-cloud", 20, 20, 1120, 870, null, "AWS Cloud"));
        diagram.Add(Group("region", "aws-region", 60, 90, 1040, 770, "cloud", "Asia Pacific (Tokyo)"));
        diagram.Add(Group("vpc", "aws-vpc", 100, 160, 960, 670, "region", "VPC", "10.0.0.0/16"));
        diagram.Add(Group("az-a", "aws-availability-zone", 140, 300, 420, 470, "vpc", "Availability Zone a"));
        diagram.Add(Group("az-c", "aws-availability-zone", 600, 300, 420, 470, "vpc", "Availability Zone c"));
        diagram.Add(Group("public-a", "aws-public-subnet", 170, 350, 360, 190, "az-a", "Public subnet", "10.0.1.0/24"));
        diagram.Add(Group("private-a", "aws-private-subnet", 170, 560, 360, 190, "az-a", "Private subnet", "10.0.2.0/24"));
        diagram.Add(Group("public-c", "aws-public-subnet", 630, 350, 360, 190, "az-c", "Public subnet", "10.0.3.0/24"));
        diagram.Add(Group("private-c", "aws-private-subnet", 630, 560, 360, 190, "az-c", "Private subnet", "10.0.4.0/24"));
        diagram.Add(Icon("alb", "Arch_Elastic-Load-Balancing", 540, 190, "vpc", "Application Load Balancer"));
        diagram.Add(Icon("web-a", "Arch_Amazon-EC2", 310, 400, "public-a", "Web server"));
        diagram.Add(Icon("web-c", "Arch_Amazon-EC2", 770, 400, "public-c", "Web server"));
        diagram.Add(Icon("db-a", "Arch_Amazon-RDS", 310, 610, "private-a", "Primary"));
        diagram.Add(Icon("db-c", "Arch_Amazon-RDS", 770, 610, "private-c", "Standby"));
        diagram.Add(new DiagramGroup { Id = "scaling", Type = "aws-auto-scaling-group", Kind = DiagramGroupKind.Overlay, MemberIds = ["web-a", "web-c"], Label = "Auto Scaling group" });
        diagram.Connect(Link("alb-web-a", "alb", "web-a", "aws-arrow"));
        diagram.Connect(Link("alb-web-c", "alb", "web-c", "aws-arrow"));
        diagram.Connect(Link("web-a-db", "web-a", "db-a", "aws-arrow"));
        diagram.Connect(Link("web-c-db", "web-c", "db-a", "aws-arrow"));
        diagram.Connect(Link("db-replication", "db-a", "db-c", "aws-two-way"));
        diagram.ClearHistory();
        return diagram;
    }

    private static string? FindIcon(DiagramIconSet? icons, string[] names)
    {
        if ((icons is null) || (names.Length == 0))
        {
            return null;
        }

        var groupIcons = icons.Icons.Where(static x => x.Category == GroupCategory).ToList();
        var icon = names.Select(name => groupIcons.FirstOrDefault(x => string.Equals(x.Id, name, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(static x => x is not null)
            ?? groupIcons.FirstOrDefault(x => x.Id.Contains(names[^1], StringComparison.OrdinalIgnoreCase));
        return icon is null ? null : $"{Prefix}:{icon.Id}";
    }

    private static string Words(string name) => name.Replace('-', ' ').Replace('_', ' ');

    private static string? FolderCategory(string[] segments, string prefix) =>
        segments[..^1].LastOrDefault(x => x.StartsWith(prefix, StringComparison.Ordinal)) is { } folder ? Words(folder[prefix.Length..]) : null;

    private static DiagramGroup Group(string id, string type, double x, double y, double width, double height, string? parentId, string label, string? note = null) =>
        new()
        {
            Id = id,
            Type = type,
            Bounds = new DiagramRect(x, y, width, height),
            ParentId = parentId,
            Label = label,
            Texts = note is null ? [] : ImmutableDictionary<string, string>.Empty.Add("note", note)
        };

    private static DiagramNode Icon(string id, string icon, double x, double y, string parentId, string label) =>
        new() { Id = id, Type = $"{Prefix}:{icon}", Bounds = new DiagramRect(x, y, 80, 80), ParentId = parentId, Label = label };

    private static DiagramLink Link(string id, string source, string target, string type) =>
        new() { Id = id, Type = type, Source = new DiagramLinkEnd(source), Target = new DiagramLinkEnd(target) };

    private sealed record GroupStyle(string Name, string DisplayName, string Color, DiagramDash Dash, DiagramGroupHeader Header, string[] Icons);
}
