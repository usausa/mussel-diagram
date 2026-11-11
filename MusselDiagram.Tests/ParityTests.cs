namespace MusselDiagram;

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

using MusselDiagram.Internal;

public sealed class ParityTests
{
    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes =
        [
            new DiagramNodeType { Name = "box", DisplayName = "Web server", Category = "compute", SearchWords = ["ec2", "instance"] },
            new DiagramNodeType { Name = "locked", DisplayName = "Server" },
            new DiagramNodeType
            {
                Name = "svc",
                Ports =
                [
                    new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Side = DiagramSide.Left, Kind = "http", MaxLinks = 1 },
                    new DiagramPortDefinition { Id = "out", Direction = DiagramPortDirection.Out, Side = DiagramSide.Right, Kind = "http" },
                    new DiagramPortDefinition { Id = "sql", Direction = DiagramPortDirection.Out, Side = DiagramSide.Bottom, Kind = "sql", Accepts = ["db"] },
                    new DiagramPortDefinition { Id = "top", Side = DiagramSide.Top, Ratio = 0.25, Offset = 4, Rejects = ["box"] }
                ]
            },
            new DiagramNodeType { Name = "db", Ports = [new DiagramPortDefinition { Id = "in", Direction = DiagramPortDirection.In, Kind = "sql", Side = DiagramSide.Left }], ConnectsAsWhole = true }
        ],
        GroupTypes = [new DiagramGroupType { Name = "zone", DisplayName = "Availability zone", Category = "network" }],
        Categories = [new DiagramCategory { Name = "compute", DisplayName = "Compute" }, new DiagramCategory { Name = "network", DisplayName = "Network" }],
        LinkTypes =
        [
            new DiagramLinkType { Name = "ortho" },
            new DiagramLinkType { Name = "line", Form = DiagramLineForm.Straight, TargetMarker = DiagramMarker.None },
            new DiagramLinkType { Name = "curve", Form = DiagramLineForm.Curved, SourceMarker = DiagramMarker.Circle, StubLength = 30 },
            new DiagramLinkType { Name = "flow", AllowCycles = false, AllowCrossParent = false, AllowSelf = true, AllowDuplicate = true }
        ],
        DefaultLinkType = "ortho"
    };

    [Fact]
    public async Task ScriptAndCoreAgree()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramGroup { Id = "zone", Type = "zone", Bounds = new DiagramRect(600, 600, 500, 300) });
        diagram.Add(new DiagramNode { Id = "a", Type = "box", Bounds = new DiagramRect(0, 0, 100, 50) });
        diagram.Add(new DiagramNode { Id = "b", Type = "box", Bounds = new DiagramRect(300.125, 100.375, 100, 50) });
        diagram.Add(new DiagramNode { Id = "s1", Type = "svc", Bounds = new DiagramRect(0, 300, 120, 61) });
        diagram.Add(new DiagramNode { Id = "s2", Type = "svc", Bounds = new DiagramRect(400.5, 500.25, 120, 60) });
        diagram.Add(new DiagramNode { Id = "d", Type = "db", Bounds = new DiagramRect(-300, 200, 80, 80) });
        diagram.Add(new DiagramNode { Id = "z", Type = "box", Bounds = new DiagramRect(640, 660, 100, 50), ParentId = "zone" });
        diagram.Add(new DiagramNode { Id = "l", Type = "locked", Bounds = new DiagramRect(-200, -300, 100, 50), Locked = true });
        diagram.Add(new DiagramNode { Id = "u", Type = "unknown", Bounds = new DiagramRect(200, -300, 100, 50) });
        diagram.Connect(new DiagramLink { Id = "taken", Type = "ortho", Source = new DiagramLinkEnd("s2", "out"), Target = new DiagramLinkEnd("s1", "in") });
        diagram.Connect(new DiagramLink { Id = "ab", Type = "flow", Source = new DiagramLinkEnd("a"), Target = new DiagramLinkEnd("b") });
        diagram.Connect(new DiagramLink { Id = "dup", Type = "ortho", Source = new DiagramLinkEnd("a"), Target = new DiagramLinkEnd("b") });
        var snapshot = diagram.Snapshot;
        var index = diagram.Index;
        var context = new RenderContext(index, "parity", true);

        var ends = snapshot.Nodes.SelectMany(box => index.PortsOf(box).Select(x => new DiagramLinkEnd(box.Id, x.Id)).Append(new DiagramLinkEnd(box.Id))).ToList();
        var routes = new JsonArray();
        foreach (var source in ends)
        {
            foreach (var target in ends)
            {
                if (string.Equals(source.ElementId, target.ElementId, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var type in Schema.LinkTypes)
                {
                    routes.Add(Route(snapshot, index, source, target, type));
                }
            }
        }

        var rules = new JsonArray();
        foreach (var source in ends)
        {
            foreach (var target in ends)
            {
                foreach (var linkType in new[] { "ortho", "flow" })
                {
                    var reason = DiagramRules.CheckConnection(snapshot, index, source, target, linkType, static _ => true, null, true);
                    rules.Add(new JsonObject
                    {
                        ["source"] = End(source),
                        ["target"] = End(target),
                        ["linkType"] = linkType,
                        ["expected"] = reason is null ? null : JsonNamingPolicy.CamelCase.ConvertName(reason.Kind.ToString())
                    });
                }
            }
        }

        var numbers = new JsonArray();
        foreach (var value in new[] { 0.125, 0.135, 2.675, 1.005, -0.125, -1.005, 12.345, 0.0001, 100.995, -0.004, 300.125, 0.5, -2.5 })
        {
            numbers.Add(new JsonObject { ["value"] = value, ["expected"] = LinkRouting.Format(value) });
        }

        var searches = new JsonArray();
        var boxTypes = Schema.NodeTypes.Cast<DiagramBoxType>().Concat(Schema.GroupTypes).ToList();
        foreach (var query in new[] { "server", "SERVER", "ec2", "comp", "net zone", "web serv", "db", " ", "x" })
        {
            searches.Add(new JsonObject
            {
                ["query"] = query,
                ["names"] = new JsonArray([.. boxTypes.Select(static x => JsonValue.Create(x.Name))]),
                ["expected"] = new JsonArray([.. TypeSearch.Search(boxTypes, query, index, null).Select(static x => JsonValue.Create(x.Name))])
            });
        }

        var data = new JsonObject
        {
            ["table"] = JsonNode.Parse(ScriptJson.ToElement(ScriptTables.Build(snapshot, index)).GetRawText()),
            ["schema"] = JsonNode.Parse(ScriptJson.ToElement(ScriptTables.Schema(context)).GetRawText()),
            ["routes"] = routes,
            ["rules"] = rules,
            ["numbers"] = numbers,
            ["searches"] = searches
        };
        var file = Path.Combine(Path.GetTempPath(), $"mussel-diagram-parity-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(file, data.ToJsonString(), Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            var (exitCode, log) = await RunNodeAsync(file).ConfigureAwait(true);
            if (exitCode is null)
            {
                Assert.Skip("node が見つからないため、一致のテストを省略しました");
                return;
            }

            Assert.True(exitCode == 0, log);
            Assert.True(routes.Count > 500);
            Assert.Contains(rules, x => x?["expected"] is null);
            Assert.Contains(rules, x => (string?)x?["expected"] == "limit");
            Assert.Contains(rules, x => (string?)x?["expected"] == "cycle");
            Assert.Contains(searches, x => x?["expected"]?.AsArray().Count == 2);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static JsonObject Route(DiagramSnapshot snapshot, SchemaIndex index, DiagramLinkEnd source, DiagramLinkEnd target, DiagramLinkType type)
    {
        var from = (DiagramBox)snapshot.Find(source.ElementId)!;
        var to = (DiagramBox)snapshot.Find(target.ElementId)!;
        var (start, end) = LinkRouting.Anchors(from.Bounds, index.FindPort(from, source.PortId), to.Bounds, index.FindPort(to, target.PortId));
        var (sourceCut, targetCut) = LinkRouting.Cuts(type);
        return new JsonObject
        {
            ["link"] = new JsonObject
            {
                ["source"] = source.ElementId,
                ["sourcePort"] = source.PortId,
                ["target"] = target.ElementId,
                ["targetPort"] = target.PortId,
                ["form"] = ScriptTables.FormName(type.Form),
                ["stub"] = type.StubLength,
                ["sourceCut"] = sourceCut,
                ["targetCut"] = targetCut
            },
            ["expected"] = LinkRouting.ToPath(LinkRouting.Simple(start, end, type), type.Form, sourceCut, targetCut)
        };
    }

    private static JsonObject End(DiagramLinkEnd end) => new() { ["element"] = end.ElementId, ["port"] = end.PortId };

    private static async Task<(int? ExitCode, string Log)> RunNodeAsync(string file)
    {
        var startInfo = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = TestPaths.Root
        };
        startInfo.ArgumentList.Add(Path.Combine("MusselDiagram.Tests", "js", "parity.mjs"));
        startInfo.ArgumentList.Add(file);
        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception)
        {
            return (null, string.Empty);
        }

        if (process is null)
        {
            return (null, string.Empty);
        }

        using (process)
        {
            var token = Xunit.TestContext.Current.CancellationToken;
            var output = process.StandardOutput.ReadToEndAsync(token);
            var errors = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token).ConfigureAwait(true);
            return (process.ExitCode, await output.ConfigureAwait(true) + await errors.ConfigureAwait(true));
        }
    }
}
