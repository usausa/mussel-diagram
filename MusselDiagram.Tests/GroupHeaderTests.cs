namespace MusselDiagram;

public sealed class GroupHeaderTests : BunitContext
{
    private static readonly DiagramSchema Schema = new()
    {
        GroupTypes =
        [
            new DiagramGroupType
            {
                Name = "vpc",
                Look = new DiagramLook { Preset = "vpc" },
                Header = new DiagramGroupHeader { IconSize = 40, NameLeft = 52.8, NameTop = 9.6 }
            },
            new DiagramGroupType
            {
                Name = "scaling",
                Look = new DiagramLook { Icon = "test:box", Dash = DiagramDash.Dashed },
                Header = new DiagramGroupHeader { Form = DiagramHeaderForm.TopCenter, IconSize = 40 }
            },
            new DiagramGroupType
            {
                Name = "zone",
                Look = new DiagramLook { Icon = "test:box" },
                Header = new DiagramGroupHeader { Form = DiagramHeaderForm.NameOnly, NameAlignment = DiagramTextAlignment.Center }
            }
        ],
        Presets = [new DiagramPreset { Name = "vpc", Look = new DiagramLook { Icon = "test:box", Stroke = "#8c4fff" } }],
        IconSets = [new DiagramIconSet { Prefix = "test", Icons = [new DiagramIcon { Id = "box", Body = "<rect width=\"24\" height=\"24\" />" }] }]
    };

    public GroupHeaderTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public void HeadersPlaceTheIconAndTheNameByTheirForm()
    {
        var diagram = new Diagram(Schema);
        diagram.Add(new DiagramGroup { Id = "v", Type = "vpc", Bounds = new DiagramRect(0, 0, 400, 300), Label = "VPC" });
        diagram.Add(new DiagramGroup { Id = "s", Type = "scaling", Bounds = new DiagramRect(500, 0, 300, 200), Label = "Auto Scaling group" });
        diagram.Add(new DiagramGroup { Id = "z", Type = "zone", Bounds = new DiagramRect(0, 400, 300, 200), Label = "Availability Zone" });

        var cut = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, diagram));

        var vpc = cut.Find("g.mussel-diagram-group[data-id='v']");
        Assert.Contains("--mussel-diagram-shape-stroke: #8c4fff;", vpc.GetAttribute("style"), StringComparison.Ordinal);
        var vpcIcon = vpc.QuerySelector("use.mussel-diagram-icon")!;
        Assert.Null(vpcIcon.GetAttribute("x"));
        Assert.Equal("40", vpcIcon.GetAttribute("width"));
        var vpcName = vpc.QuerySelector("text.mussel-diagram-label")!;
        Assert.Equal(("52.8", "9.6", "start"), (vpcName.GetAttribute("x"), vpcName.GetAttribute("y"), vpcName.GetAttribute("text-anchor")));

        var scaling = cut.Find("g.mussel-diagram-group[data-id='s']");
        var scalingIcon = scaling.QuerySelector("use.mussel-diagram-icon")!;
        Assert.Equal(("130", "-20"), (scalingIcon.GetAttribute("x"), scalingIcon.GetAttribute("y")));
        var scalingName = scaling.QuerySelector("text.mussel-diagram-label")!;
        Assert.Equal(("150", "26", "middle"), (scalingName.GetAttribute("x"), scalingName.GetAttribute("y"), scalingName.GetAttribute("text-anchor")));

        var zone = cut.Find("g.mussel-diagram-group[data-id='z']");
        Assert.Null(zone.QuerySelector("use.mussel-diagram-icon"));
        var zoneName = zone.QuerySelector("text.mussel-diagram-label")!;
        Assert.Equal(("150", "6", "middle"), (zoneName.GetAttribute("x"), zoneName.GetAttribute("y"), zoneName.GetAttribute("text-anchor")));
        Assert.Equal("288", zoneName.GetAttribute("data-width"));
    }
}
