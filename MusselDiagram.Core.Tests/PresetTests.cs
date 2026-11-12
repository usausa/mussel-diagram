namespace MusselDiagram;

using MusselDiagram.Internal;

public sealed class PresetTests
{
    [Fact]
    public void TypesTakeTheLookOfTheirPresetAndKeepTheirOwnValues()
    {
        var vpc = new DiagramGroupType { Name = "vpc", Look = new DiagramLook { Preset = "network", Fill = "#ffffff" } };
        var subnet = new DiagramGroupType { Name = "subnet", Look = new DiagramLook { Preset = "network", Dash = DiagramDash.Dotted, Stroke = "#00a4a6" } };
        var plain = new DiagramGroupType { Name = "plain", Look = new DiagramLook { Preset = "missing", Stroke = "#000000" } };
        var arrow = new DiagramLinkType { Name = "arrow", Look = new DiagramLook { Preset = "line" } };
        var index = SchemaIndex.For(new DiagramSchema
        {
            GroupTypes = [vpc, subnet, plain],
            LinkTypes = [arrow],
            Presets =
            [
                new DiagramPreset { Name = "network", Look = new DiagramLook { Stroke = "#8c4fff", StrokeWidth = 1.67, Dash = DiagramDash.Dashed, Icon = "aws:vpc" } },
                new DiagramPreset { Name = "line", Look = new DiagramLook { Stroke = "var(--app-line)", StrokeWidth = 1.67 } }
            ]
        });

        var vpcLook = index.LookOf(vpc);
        Assert.Equal("#8c4fff", vpcLook.Stroke);
        Assert.Equal("#ffffff", vpcLook.Fill);
        Assert.Equal(1.67, vpcLook.StrokeWidth);
        Assert.Equal(DiagramDash.Dashed, vpcLook.Dash);
        Assert.Equal("aws:vpc", vpcLook.Icon);

        var subnetLook = index.LookOf(subnet);
        Assert.Equal("#00a4a6", subnetLook.Stroke);
        Assert.Equal(DiagramDash.Dotted, subnetLook.Dash);

        Assert.Same(plain.Look, index.LookOf(plain));
        Assert.Equal("var(--app-line)", index.LookOf(arrow).Stroke);
    }
}
