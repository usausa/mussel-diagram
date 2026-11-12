namespace MusselDiagram;

public sealed class ValidationTests
{
    [Fact]
    public void ConnectionRulesGiveTheFirstReason()
    {
        var schema = new DiagramSchema
        {
            NodeTypes =
            [
                new DiagramNodeType { Name = "box" },
                new DiagramNodeType
                {
                    Name = "svc",
                    Ports =
                    [
                        new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left, Kind = "http", MaxLinks = 1 },
                        new DiagramPortDefinition { Id = "out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right, Kind = "http" },
                        new DiagramPortDefinition { Id = "sql", Direction = DiagramPortDirection.Out, Side = DiagramSide.Bottom, Kind = "sql", Accepts = ["db"] }
                    ]
                },
                new DiagramNodeType { Name = "db", Ports = [new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left, Kind = "sql" }] },
                new DiagramNodeType { Name = "cache", Ports = [new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left, Kind = "sql" }] }
            ],
            GroupTypes = [new DiagramGroupType { Name = "zone" }],
            LinkTypes =
            [
                new DiagramLinkType { Name = "arrow" },
                new DiagramLinkType { Name = "flow", AllowCrossParent = false, AllowCycles = false }
            ],
            Rules = new DiagramAppRules { CanConnect = static x => x.Source.ElementId == "app" ? DiagramRuleResult.Reject("No links from app.") : DiagramRuleResult.Allow }
        };
        var diagram = new Diagram(schema);
        diagram.Add(new DiagramGroup { Id = "zone", Type = "zone", Bounds = new DiagramRect(0, 1000, 400, 300) });
        foreach (var (id, type) in new[] { ("s1", "svc"), ("s2", "svc"), ("s3", "svc"), ("s4", "svc"), ("db", "db"), ("cache", "cache"), ("b1", "box"), ("b2", "box"), ("b3", "box"), ("b4", "box"), ("app", "box") })
        {
            diagram.Add(new DiagramNode { Id = id, Type = type, Bounds = new DiagramRect(0, 0, 100, 50) });
        }

        diagram.Add(new DiagramNode { Id = "zb", Type = "box", Bounds = new DiagramRect(20, 1040, 100, 50), ParentId = "zone" });
        diagram.Add(new DiagramNode { Id = "locked", Type = "box", Bounds = new DiagramRect(0, 0, 100, 50), Locked = true });
        Connect("whole", "s1", null, "s2", "in");
        Connect("direction", "s4", "in", "s3", "in");
        Connect("kind", "s1", "out", "db", "in");
        Connect("limit1", "s2", "out", "s1", "in");
        Connect("limit2", "s3", "out", "s1", "in");
        Connect("self", "b1", null, "b1", null);
        Connect("dup1", "b1", null, "b2", null);
        Connect("dup2", "b1", null, "b2", null);
        Connect("peer", "s2", "sql", "cache", "in");
        Connect("peerok", "s3", "sql", "db", "in");
        Connect("cross", "b3", null, "zb", null, "flow");
        Connect("cycle1", "b3", null, "b4", null, "flow");
        Connect("cycle2", "b4", null, "b3", null, "flow");
        Connect("fromApp", "app", null, "b1", null);
        Connect("fromLocked", "locked", null, "b2", null);

        var issues = diagram.Validate().Where(x => x.Kind == DiagramIssueKind.ConnectionRule).ToDictionary(x => x.ElementIds[0], x => x.Arguments[0]);

        Assert.Equal(Reason(DiagramReasonKind.NoConnectionPoint), issues["whole"]);
        Assert.Equal(Reason(DiagramReasonKind.Direction), issues["direction"]);
        Assert.Equal(Reason(DiagramReasonKind.PortKind), issues["kind"]);
        Assert.Equal(Reason(DiagramReasonKind.Limit), issues["limit2"]);
        Assert.Equal(Reason(DiagramReasonKind.SelfOrDuplicate), issues["self"]);
        Assert.Equal(Reason(DiagramReasonKind.SelfOrDuplicate), issues["dup2"]);
        Assert.Equal(Reason(DiagramReasonKind.PeerType), issues["peer"]);
        Assert.Equal(Reason(DiagramReasonKind.CrossParent), issues["cross"]);
        Assert.Equal(Reason(DiagramReasonKind.Cycle), issues["cycle1"]);
        Assert.Equal(Reason(DiagramReasonKind.Cycle), issues["cycle2"]);
        Assert.Equal("No links from app.", issues["fromApp"]);
        Assert.Equal(11, issues.Count);

        void Connect(string id, string source, string? sourcePort, string target, string? targetPort, string type = "arrow") =>
            diagram.Connect(new DiagramLink { Id = id, Type = type, Source = new DiagramLinkEnd(source, sourcePort), Target = new DiagramLinkEnd(target, targetPort) });
    }

    [Fact]
    public void NestingAndCountsAreChecked()
    {
        var schema = new DiagramSchema
        {
            NodeTypes =
            [
                new DiagramNodeType { Name = "db", TopLevel = false, AllowedParents = ["subnet"], MaxCount = 1 },
                new DiagramNodeType { Name = "svc" },
                new DiagramNodeType { Name = "special" },
                new DiagramNodeType { Name = "monitor", Required = true }
            ],
            GroupTypes =
            [
                new DiagramGroupType { Name = "vpc" },
                new DiagramGroupType { Name = "subnet", AllowedChildren = ["db"] }
            ],
            Rules = new DiagramAppRules { CanNest = static x => x.Type == "special" ? DiagramRuleResult.Reject("Not here.") : DiagramRuleResult.Allow }
        };
        var diagram = new Diagram(schema);
        diagram.Add(new DiagramGroup { Id = "vpc", Type = "vpc", Bounds = new DiagramRect(0, 0, 1000, 1000) });
        diagram.Add(new DiagramGroup { Id = "subnet", Type = "subnet", Bounds = new DiagramRect(20, 40, 600, 600), ParentId = "vpc" });
        diagram.Add(new DiagramNode { Id = "top", Type = "db", Bounds = new DiagramRect(2000, 0, 100, 50) });
        diagram.Add(new DiagramNode { Id = "ok", Type = "db", Bounds = new DiagramRect(40, 80, 100, 50), ParentId = "subnet" });
        diagram.Add(new DiagramNode { Id = "wrong", Type = "db", Bounds = new DiagramRect(700, 80, 100, 50), ParentId = "vpc" });
        diagram.Add(new DiagramNode { Id = "svc", Type = "svc", Bounds = new DiagramRect(200, 80, 100, 50), ParentId = "subnet" });
        diagram.Add(new DiagramNode { Id = "special", Type = "special", Bounds = new DiagramRect(2000, 200, 100, 50) });

        var issues = diagram.Validate();

        Assert.Equal(["top", "wrong", "svc", "special"], issues.Where(x => x.Kind == DiagramIssueKind.NestingRule).Select(x => x.ElementIds[0]));
        Assert.Equal("Not here.", issues.Single(x => x.ElementIds.Contains("special")).Arguments[0]);
        var count = Assert.Single(issues, x => x.Kind == DiagramIssueKind.MaxCount);
        Assert.Equal(["top", "ok", "wrong"], count.ElementIds);
        Assert.Equal(["db", "1"], count.Arguments);
        var required = Assert.Single(issues, x => x.Kind == DiagramIssueKind.RequiredType);
        Assert.Equal(["monitor"], required.Arguments);
        Assert.Empty(required.ElementIds);
        Assert.All(issues, x => Assert.Equal(DiagramIssueSeverity.Error, x.Severity));
    }

    [Fact]
    public void PortsSharedItemsAndTypesAreChecked()
    {
        var schema = new DiagramSchema
        {
            NodeTypes =
            [
                new DiagramNodeType
                {
                    Name = "svc",
                    Ports =
                    [
                        new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Required = true },
                        new DiagramPortDefinition { Id = "out", Direction = DiagramPortDirection.Out },
                        new DiagramPortDefinition { Id = "old", Direction = DiagramPortDirection.Out }
                    ]
                },
                new DiagramNodeType
                {
                    Name = "user",
                    DataType = TestJsonContext.Default.ServerData,
                    GetReferences = static data => data is ServerData { ConfigId: { } id } ? [id] : []
                }
            ],
            SharedItemTypes = [new DiagramSharedItemType { Name = "config" }],
            Rules = new DiagramAppRules { Check = static _ => [new DiagramIssue(DiagramIssueSeverity.Warning, DiagramIssueKind.App, "Custom") { ElementIds = ["s1"] }] }
        };
        var diagram = new Diagram(schema);
        diagram.Add(new DiagramNode { Id = "s1", Type = "svc", Bounds = new DiagramRect(0, 0, 100, 50) });
        diagram.Add(new DiagramNode { Id = "s2", Type = "svc", Bounds = new DiagramRect(300, 0, 100, 50) });
        diagram.Connect(new DiagramLink { Id = "l1", Type = "arrow", Source = new DiagramLinkEnd("s2", "old"), Target = new DiagramLinkEnd("s1", "in") });
        diagram.Add(new DiagramSharedItem { Id = "cfg1", Type = "config" });
        diagram.Add(new DiagramNode { Id = "u1", Type = "user", Bounds = new DiagramRect(0, 200, 100, 50), Data = new ServerData("u1", "cfg1") });
        diagram.Add(new DiagramNode { Id = "u2", Type = "user", Bounds = new DiagramRect(200, 200, 100, 50), Data = new ServerData("u2", "cfg2") });
        diagram.Add(new DiagramNode { Id = "ghost", Type = "ghost", Bounds = new DiagramRect(400, 200, 100, 50) });

        var issues = diagram.Validate();

        var port = Assert.Single(issues, x => x.Kind == DiagramIssueKind.RequiredPort);
        Assert.Equal(["s2"], port.ElementIds);
        Assert.Equal("in", port.PortId);
        var missing = Assert.Single(issues, x => x.Kind == DiagramIssueKind.MissingSharedItem);
        Assert.Equal(["u2"], missing.ElementIds);
        Assert.Equal(["cfg2"], missing.Arguments);
        var unregistered = issues.Where(x => x.Kind == DiagramIssueKind.UnregisteredType).ToList();
        Assert.Equal(["ghost", "l1"], unregistered.Select(x => x.ElementIds[0]));
        Assert.All(unregistered, x => Assert.Equal(DiagramIssueSeverity.Warning, x.Severity));
        Assert.Equal("Custom", Assert.Single(issues, x => x.Kind == DiagramIssueKind.App).Message);
        Assert.DoesNotContain(issues, x => x.Kind == DiagramIssueKind.MissingPort);

        diagram.Schema = schema with { NodeTypes = [schema.NodeTypes[0] with { Ports = schema.NodeTypes[0].Ports[..2] }, schema.NodeTypes[1]] };
        issues = diagram.Validate();

        var lost = Assert.Single(issues, x => x.Kind == DiagramIssueKind.MissingPort);
        Assert.Equal(["l1", "s2"], lost.ElementIds);
        Assert.Equal("old", lost.PortId);
        Assert.Equal(issues.Length, diagram.Snapshot.Validate(diagram.Schema).Length);
    }

    private static string? Reason(DiagramReasonKind kind) => DiagramCultures.Reason(kind);
}
