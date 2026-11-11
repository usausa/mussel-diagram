namespace MusselDiagram;

using static MusselDiagram.TestDiagrams;

public sealed class GeometryTests
{
    [Fact]
    public void PortsComeFromTheTypeAndTheBounds()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("s", 100, 200, type: "service"));

        var ports = diagram.GetPorts("s").ToDictionary(x => x.Id);

        Assert.Equal(new DiagramPoint(100, 225), ports["in"].Position);
        Assert.Equal(new DiagramPoint(200, 225), ports["out"].Position);
        Assert.Equal(new DiagramPoint(125, 200), ports["top"].Position);
        Assert.Equal(new DiagramPoint(150, 254), ports["bottom"].Position);
        Assert.Equal(DiagramPortDirection.In, ports["in"].Direction);
        Assert.Equal(DiagramSide.Bottom, ports["bottom"].Side);
        Assert.Null(ports["out"].Kind);
        Assert.Null(ports["out"].Label);
        Assert.Empty(diagram.GetPorts("missing"));
    }

    [Fact]
    public void WholeElementLinksTakeTheFacingSides()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 300, 0));
        diagram.Add(Node("c", 0, 300));

        diagram.Connect(Link("ab", "a", "b"));
        diagram.Connect(Link("ac", "a", "c"));

        Assert.Equal([new DiagramPoint(100, 25), new DiagramPoint(300, 25)], ((DiagramLink)diagram.Find("ab")!).Route);
        Assert.Equal([new DiagramPoint(50, 50), new DiagramPoint(50, 300)], ((DiagramLink)diagram.Find("ac")!).Route);
        Assert.Equal(DiagramRouteState.Routed, ((DiagramLink)diagram.Find("ab")!).RouteState);
    }

    [Fact]
    public void PortLinksBendInTheMiddle()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0, type: "service"));
        diagram.Add(Node("b", 300, 100, type: "service"));
        diagram.Add(Node("c", 300, 400, type: "service"));

        diagram.Connect(Link("ab", "a", "b", "out", "in"));
        diagram.Connect(Link("ac", "a", "c", "out", "top"));

        Assert.Equal(
            [new DiagramPoint(100, 25), new DiagramPoint(200, 25), new DiagramPoint(200, 125), new DiagramPoint(300, 125)],
            ((DiagramLink)diagram.Find("ab")!).Route);
        Assert.Equal(
            [new DiagramPoint(100, 25), new DiagramPoint(290, 25), new DiagramPoint(290, 380), new DiagramPoint(325, 380), new DiagramPoint(325, 400)],
            ((DiagramLink)diagram.Find("ac")!).Route);
    }

    [Fact]
    public void PortsWithoutARatioAreSpacedOnTheirSide()
    {
        var schema = new DiagramSchema
        {
            NodeTypes =
            [
                new DiagramNodeType
                {
                    Name = "switch",
                    Ports =
                    [
                        new DiagramPortDefinition { Id = "a", Side = DiagramSide.Right },
                        new DiagramPortDefinition { Id = "b", Side = DiagramSide.Right },
                        new DiagramPortDefinition { Id = "c", Side = DiagramSide.Right, Ratio = 0.1 }
                    ]
                }
            ]
        };
        var diagram = new Diagram(schema);
        diagram.Add(new DiagramNode { Id = "s", Type = "switch", Bounds = new DiagramRect(0, 0, 100, 90) });

        Assert.Equal([30, 60, 9], diagram.GetPorts("s").Select(static x => x.Position.Y));
    }

    [Fact]
    public void TypesHaveADefaultSize()
    {
        var size = new DiagramNodeType { Name = "plain" }.Size;

        Assert.Equal(120, size.Width);
        Assert.Equal(60, size.Height);
    }

    [Fact]
    public void StraightLinksJoinTheEnds()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 300, 100));

        diagram.Connect(Link("ab", "a", "b", type: "line"));

        var geometry = diagram.GetLinkGeometry("ab");
        Assert.NotNull(geometry);
        Assert.Equal([new DiagramPoint(100, 25), new DiagramPoint(300, 125)], geometry.Points);
        Assert.Equal(new DiagramPoint(100, 25), geometry.SourcePoint);
        Assert.Equal(new DiagramPoint(300, 125), geometry.TargetPoint);
        Assert.Equal("M100,25L291.06,120.53", geometry.Path);
        Assert.Equal(26.57, geometry.TargetAngle);
        Assert.Equal(-153.43, geometry.SourceAngle);
        Assert.Null(diagram.GetLinkGeometry("a"));
    }
}
