namespace MusselDiagram;

using System.Text.Json;

using static MusselDiagram.TestDiagrams;

public sealed class DataTests
{
    private static readonly DiagramSchema DataSchema = Schema with
    {
        NodeTypes =
        [
            .. Schema.NodeTypes,
            new DiagramNodeType { Name = "typed", DataType = TestJsonContext.Default.ServerData },
            new DiagramNodeType
            {
                Name = "switch",
                DataType = TestJsonContext.Default.SwitchData,
                GetPorts = static box => box.Data is SwitchData data
                    ? [new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left }, .. data.Outputs.Select(static x => new DiagramPortDefinition { Id = x, Direction = DiagramPortDirection.Out })]
                    : []
            }
        ],
        SharedItemTypes = [new DiagramSharedItemType { Name = "config" }]
    };

    [Fact]
    public void DataMustBeTheRegisteredTypeOrJson()
    {
        var diagram = new Diagram(DataSchema);
        using var document = JsonDocument.Parse("""{"name":"web"}""");

        diagram.Add(Node("a", 0, 0, type: "typed") with { Data = new ServerData("a") });
        diagram.Add(Node("b", 0, 0, type: "typed") with { Data = document.RootElement.Clone() });
        diagram.Add(Node("c", 0, 0) with { Data = document.RootElement.Clone() });
        diagram.Add(Node("d", 0, 0, type: "unknown") with { Data = document.RootElement.Clone() });

        Assert.Throws<DiagramException>(() => diagram.Add(Node("e", 0, 0, type: "typed") with { Data = "text" }));
        Assert.Throws<DiagramException>(() => diagram.Add(Node("e", 0, 0) with { Data = new ServerData("e") }));
        Assert.Throws<DiagramException>(() => diagram.Replace(diagram.Find("a")! with { Data = new SwitchData([]) }));
        Assert.Equal(4, diagram.Snapshot.Nodes.Length);
    }

    [Fact]
    public void SharedItemsAreNeverEndsParentsOrMembers()
    {
        var diagram = new Diagram(DataSchema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(new DiagramSharedItem { Id = "cfg", Type = "config", Label = "Config" });

        Assert.Throws<DiagramException>(() => diagram.Connect(Link("l", "a", "cfg")));
        Assert.Throws<DiagramException>(() => diagram.Add(Node("b", 0, 0, "cfg")));
        Assert.Throws<DiagramException>(() => diagram.Add(new DiagramGroup { Id = "g", Type = "scaling", Kind = DiagramGroupKind.Overlay, MemberIds = ["cfg"] }));
        Assert.Equal(["cfg"], diagram.Snapshot.SharedItems.Select(x => x.Id));

        using var number = JsonDocument.Parse("1");
        diagram.Replace(diagram.Find("cfg")! with { Data = number.RootElement.Clone() });
        diagram.Delete(["cfg"]);
        Assert.Empty(diagram.Snapshot.SharedItems);
    }

    [Fact]
    public void SharedItemsAreFoundThroughTheData()
    {
        var schema = DataSchema with
        {
            NodeTypes = [.. DataSchema.NodeTypes.Select(static x => x.Name == "typed" ? x with { GetReferences = static data => data is ServerData { ConfigId: { } id } ? [id] : [] } : x)]
        };
        var diagram = new Diagram(schema);
        diagram.Add(new DiagramSharedItem { Id = "used", Type = "config" });
        diagram.Add(new DiagramSharedItem { Id = "unused", Type = "config" });
        diagram.Add(Node("a", 0, 0, type: "typed") with { Data = new ServerData("a", "used") });
        diagram.Add(Node("b", 0, 0, type: "typed") with { Data = new ServerData("b", "used") });
        diagram.Add(Node("c", 0, 0, type: "typed") with { Data = new ServerData("c") });
        using var document = JsonDocument.Parse("""{"ConfigId":"unused"}""");
        diagram.Add(Node("d", 0, 0, type: "typed") with { Data = document.RootElement.Clone() });

        Assert.Equal(["a", "b"], diagram.Snapshot.GetReferrers("used", schema).Select(static x => x.Id));
        Assert.Empty(diagram.Snapshot.GetReferrers("used", DataSchema));
        Assert.Equal(["unused"], diagram.Snapshot.GetUnusedSharedItems(schema).Select(static x => x.Id));
    }

    [Fact]
    public void LinksGoWithThePortsTheDataRemoves()
    {
        var diagram = new Diagram(DataSchema);
        diagram.Add(Node("s", 0, 0, type: "switch") with { Data = new SwitchData(["yes", "no"]) });
        diagram.Add(Node("t", 300, 0));
        diagram.Connect(Link("yes", "s", "t", sourcePort: "yes"));
        diagram.Connect(Link("no", "s", "t", sourcePort: "no"));
        Assert.Equal(["in", "yes", "no"], diagram.GetPorts("s").Select(x => x.Id));
        DiagramChangedEventArgs? change = null;
        diagram.Changed += (_, e) => change = e;

        diagram.Replace(diagram.Find("s")! with { Data = new SwitchData(["yes"]) });

        Assert.Equal(["yes"], diagram.Snapshot.Links.Select(x => x.Id));
        Assert.Equal(["no"], change!.Deleted);
        Assert.Equal(DiagramChangeParts.Data, change.GetChanges("s"));
        Assert.Equal(["in", "yes"], diagram.GetPorts("s").Select(x => x.Id));
        Assert.Throws<DiagramException>(() => diagram.Connect(Link("no", "s", "t", sourcePort: "no")));
    }
}
