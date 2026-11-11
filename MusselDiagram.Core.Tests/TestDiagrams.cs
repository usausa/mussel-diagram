namespace MusselDiagram;

internal static class TestDiagrams
{
    public static DiagramSchema Schema { get; } = new()
    {
        NodeTypes =
        [
            new DiagramNodeType { Name = "server" },
            new DiagramNodeType
            {
                Name = "service",
                Ports =
                [
                    new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left },
                    new DiagramPortDefinition { Id = "out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right },
                    new DiagramPortDefinition { Id = "top", Side = DiagramSide.Top, Ratio = 0.25 },
                    new DiagramPortDefinition { Id = "bottom", Side = DiagramSide.Bottom, Offset = 4 }
                ]
            }
        ],
        GroupTypes =
        [
            new DiagramGroupType { Name = "vpc" },
            new DiagramGroupType { Name = "subnet" },
            new DiagramGroupType { Name = "scaling", Kind = DiagramGroupKind.Overlay }
        ],
        LinkTypes =
        [
            new DiagramLinkType { Name = "arrow" },
            new DiagramLinkType { Name = "line", Form = DiagramLineForm.Straight }
        ],
        DefaultLinkType = "arrow"
    };

    public static DiagramNode Node(string id, double x, double y, string? parentId = null, string type = "server") =>
        new() { Id = id, Type = type, Bounds = new DiagramRect(x, y, 100, 50), ParentId = parentId };

    public static DiagramGroup Group(string id, double x, double y, double width, double height, string? parentId = null) =>
        new() { Id = id, Type = "vpc", Bounds = new DiagramRect(x, y, width, height), ParentId = parentId };

    public static DiagramLink Link(string id, string source, string target, string? sourcePort = null, string? targetPort = null, string type = "arrow") =>
        new() { Id = id, Type = type, Source = new DiagramLinkEnd(source, sourcePort), Target = new DiagramLinkEnd(target, targetPort) };
}
