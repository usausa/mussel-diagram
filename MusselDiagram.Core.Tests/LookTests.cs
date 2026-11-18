namespace MusselDiagram;

using System.Text.Json;

using static MusselDiagram.TestDiagrams;

public sealed class LookTests
{
    private static readonly DiagramSchema LookSchema = Schema with
    {
        LinkTypes = [.. Schema.LinkTypes, new DiagramLinkType { Name = "plain", TargetMarker = DiagramMarker.None }]
    };

    [Fact]
    public void LooksAreSavedAndLoaded()
    {
        var diagram = new Diagram(LookSchema);
        diagram.Add(Node("a", 0, 0) with { Look = new DiagramBoxLook { Fill = "#fff3e0", Stroke = "rgb(229, 57, 53)", StrokeWidth = 2, Dash = DiagramDash.Dashed, TextColor = "var(--brand-text)" } });
        diagram.Add(Node("b", 300, 0));
        diagram.Connect(Link("ab", "a", "b") with
        {
            Look = new DiagramLinkLook { Stroke = "#e53935", StrokeWidth = 3, Dash = DiagramDash.Dotted, SourceMarker = DiagramMarker.Diamond, TargetMarker = DiagramMarker.None, Form = DiagramLineForm.Curved }
        });
        var json = diagram.ToJson();

        using var document = JsonDocument.Parse(json);
        var node = document.RootElement.GetProperty("nodes")[0].GetProperty("look");
        Assert.Equal("#fff3e0", node.GetProperty("fill").GetString());
        Assert.Equal("rgb(229, 57, 53)", node.GetProperty("stroke").GetString());
        Assert.Equal(2, node.GetProperty("strokeWidth").GetDouble());
        Assert.Equal("dashed", node.GetProperty("dash").GetString());
        Assert.Equal("var(--brand-text)", node.GetProperty("textColor").GetString());
        Assert.False(document.RootElement.GetProperty("nodes")[1].TryGetProperty("look", out _));
        var link = document.RootElement.GetProperty("links")[0].GetProperty("look");
        Assert.Equal("#e53935", link.GetProperty("stroke").GetString());
        Assert.Equal(3, link.GetProperty("strokeWidth").GetDouble());
        Assert.Equal("dotted", link.GetProperty("dash").GetString());
        Assert.Equal("diamond", link.GetProperty("sourceMarker").GetString());
        Assert.Equal("none", link.GetProperty("targetMarker").GetString());
        Assert.Equal("curved", link.GetProperty("form").GetString());

        var loaded = new Diagram(LookSchema);
        Assert.Empty(loaded.LoadJson(json));
        Assert.Equal(((DiagramBox)diagram.Find("a")!).Look, ((DiagramBox)loaded.Find("a")!).Look);
        Assert.Equal(((DiagramLink)diagram.Find("ab")!).Look, ((DiagramLink)loaded.Find("ab")!).Look);
        Assert.Equal(json, loaded.ToJson());
    }

    [Fact]
    public void ChangingTheFormOfALinkRoutesItAgainAndUndoRestoresIt()
    {
        var diagram = new Diagram(LookSchema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 300, 200));
        diagram.Connect(Link("ab", "a", "b"));
        var orthogonal = ((DiagramLink)diagram.Find("ab")!).Route;
        DiagramChangedEventArgs? change = null;
        diagram.Changed += (_, e) => change = e;

        diagram.Replace((DiagramLink)diagram.Find("ab")! with { Look = new DiagramLinkLook { Form = DiagramLineForm.Straight } });

        Assert.True(orthogonal.Length > 2);
        Assert.Equal(2, ((DiagramLink)diagram.Find("ab")!).Route.Length);
        Assert.Equal(DiagramChangeParts.Look | DiagramChangeParts.Route, change!.GetChanges("ab"));
        diagram.Undo();
        Assert.Null(((DiagramLink)diagram.Find("ab")!).Look);
        Assert.True(orthogonal.SequenceEqual(((DiagramLink)diagram.Find("ab")!).Route));
    }

    [Fact]
    public void LooksReportTheirOwnChangedPart()
    {
        var diagram = new Diagram(LookSchema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 300, 0));
        diagram.Connect(Link("ab", "a", "b"));
        DiagramChangedEventArgs? change = null;
        diagram.Changed += (_, e) => change = e;

        diagram.Replace((DiagramNode)diagram.Find("a")! with { Look = new DiagramBoxLook { Fill = "#ffcdd2" } });
        Assert.Equal(DiagramChangeParts.Look, change!.GetChanges("a"));

        diagram.Replace((DiagramLink)diagram.Find("ab")! with { Look = new DiagramLinkLook { Stroke = "#e53935" } });
        Assert.Equal(DiagramChangeParts.Look, change.GetChanges("ab"));
    }

    [Fact]
    public void RoutersAreToldTheFormOfTheLink()
    {
        var router = new FormRouter();
        var diagram = new Diagram(LookSchema with { Router = router });
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 300, 200));
        diagram.Connect(Link("ab", "a", "b"));

        diagram.Replace((DiagramLink)diagram.Find("ab")! with { Look = new DiagramLinkLook { Form = DiagramLineForm.Curved } });

        Assert.Equal([DiagramLineForm.Orthogonal, DiagramLineForm.Curved], router.Forms);
    }

    [Fact]
    public void LinkGeometryFollowsTheFormAndMarkersOfTheLink()
    {
        var diagram = new Diagram(LookSchema);
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 300, 200));
        diagram.Connect(Link("ab", "a", "b") with { Look = new DiagramLinkLook { Form = DiagramLineForm.Straight, TargetMarker = DiagramMarker.None } });
        diagram.Connect(Link("plain", "a", "b", type: "plain") with { Look = new DiagramLinkLook { Form = DiagramLineForm.Straight } });

        Assert.Equal(2, ((DiagramLink)diagram.Find("ab")!).Route.Length);
        Assert.Equal(diagram.GetLinkGeometry("plain")!.Path, diagram.GetLinkGeometry("ab")!.Path);
    }

    [Theory]
    [InlineData("#abc")]
    [InlineData("#abcd")]
    [InlineData("#aabbcc")]
    [InlineData("#AABBCC80")]
    [InlineData("rgba(1, 2, 3, 0.5)")]
    [InlineData("hsl(120deg 50% 50%)")]
    [InlineData("color-mix(in srgb, red 50%, blue)")]
    [InlineData("transparent")]
    [InlineData("var(--brand-color)")]
    public void ColorsInCssSyntaxAreAccepted(string color)
    {
        var diagram = new Diagram(LookSchema);
        diagram.Add(Node("a", 0, 0) with { Look = new DiagramBoxLook { Fill = color, Stroke = color, TextColor = color } });
        diagram.Add(Node("b", 300, 0));
        diagram.Connect(Link("ab", "a", "b") with { Look = new DiagramLinkLook { Stroke = color } });

        Assert.Equal(color, ((DiagramBox)diagram.Find("a")!).Look!.Fill);
        Assert.Equal(color, ((DiagramLink)diagram.Find("ab")!).Look!.Stroke);
    }

    [Theory]
    [InlineData("#abcde")]
    [InlineData("#aabbcc8")]
    public void HexColorsOfOtherLengthsAreRejected(string color)
    {
        var diagram = new Diagram(LookSchema);

        Assert.Throws<DiagramException>(() => diagram.Add(Node("a", 0, 0) with { Look = new DiagramBoxLook { Fill = color } }));
    }

    [Theory]
    [InlineData("red; background: url(x)")]
    [InlineData("url(#gradient)")]
    [InlineData("var(--brand, red)")]
    [InlineData("expression(alert(1))")]
    [InlineData("")]
    public void ColorsThatCouldCarryOtherStylesAreRejected(string color)
    {
        var diagram = new Diagram(LookSchema);

        Assert.Throws<DiagramException>(() => diagram.Add(Node("a", 0, 0) with { Look = new DiagramBoxLook { Fill = color } }));
        Assert.Throws<DiagramException>(() => diagram.Add(Node("a", 0, 0) with { Look = new DiagramBoxLook { TextColor = color } }));
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 300, 0));
        Assert.Throws<DiagramException>(() => diagram.Connect(Link("ab", "a", "b") with { Look = new DiagramLinkLook { Stroke = color } }));
    }

    [Fact]
    public void WidthsAndKindsOutsideTheirRangeAreRejected()
    {
        var diagram = new Diagram(LookSchema);

        Assert.Throws<DiagramException>(() => diagram.Add(Node("a", 0, 0) with { Look = new DiagramBoxLook { StrokeWidth = -1 } }));
        Assert.Throws<DiagramException>(() => diagram.Add(Node("a", 0, 0) with { Look = new DiagramBoxLook { StrokeWidth = Double.NaN } }));
        Assert.Throws<DiagramException>(() => diagram.Add(Node("a", 0, 0) with { Look = new DiagramBoxLook { Dash = (DiagramDash)9 } }));
        diagram.Add(Node("a", 0, 0));
        diagram.Add(Node("b", 300, 0));
        Assert.Throws<DiagramException>(() => diagram.Connect(Link("ab", "a", "b") with { Look = new DiagramLinkLook { StrokeWidth = Double.PositiveInfinity } }));
        Assert.Throws<DiagramException>(() => diagram.Connect(Link("ab", "a", "b") with { Look = new DiagramLinkLook { TargetMarker = (DiagramMarker)9 } }));
        Assert.Throws<DiagramException>(() => diagram.Connect(Link("ab", "a", "b") with { Look = new DiagramLinkLook { Form = (DiagramLineForm)9 } }));
    }

    [Fact]
    public void LoadDropsLookValuesThatCannotBeUsed()
    {
        const string json = """
            {
              "format": "mussel-diagram",
              "version": 1,
              "nodes": [
                { "id": "a", "type": "server", "x": 0, "y": 0, "width": 100, "height": 50, "look": { "fill": "red;x:y", "stroke": "#000000", "dash": "wavy", "strokeWidth": -2 } },
                { "id": "b", "type": "server", "x": 300, "y": 0, "width": 100, "height": 50, "look": { "fill": "url(#x)" } }
              ],
              "links": [
                { "id": "ab", "type": "arrow", "source": { "element": "a" }, "target": { "element": "b" }, "look": { "targetMarker": "arrowhead", "form": "curved" } }
              ]
            }
            """;
        var diagram = new Diagram(LookSchema);

        var issues = diagram.LoadJson(json);

        Assert.Equal(new DiagramBoxLook { Stroke = "#000000" }, ((DiagramBox)diagram.Find("a")!).Look);
        Assert.Null(((DiagramBox)diagram.Find("b")!).Look);
        Assert.Equal(new DiagramLinkLook { Form = DiagramLineForm.Curved }, ((DiagramLink)diagram.Find("ab")!).Look);
        Assert.Equal(["a", "b", "ab"], issues.Select(static x => x.ElementIds.Single()));
        Assert.All(issues, static x => Assert.Equal((DiagramIssueSeverity.Warning, DiagramIssueKind.InvalidLook), (x.Severity, x.Kind)));
    }

    private sealed class FormRouter : IDiagramRouter
    {
        public List<DiagramLineForm> Forms { get; } = [];

        public ValueTask<DiagramRouteResult> RouteAsync(DiagramRouteRequest request, CancellationToken cancellationToken)
        {
            Forms.Add(request.Form);
            return ValueTask.FromResult(new DiagramRouteResult { Points = [request.Source.Position, request.Target.Position] });
        }
    }
}
