namespace MusselDiagram;

using System.Text.Json;

public sealed class LanguageViewTests : BunitContext
{
    private static readonly DiagramSchema Schema = new()
    {
        NodeTypes = [new DiagramNodeType { Name = "server", DisplayName = "Server" }],
        LinkTypes = [new DiagramLinkType { Name = "arrow" }]
    };

    private readonly BunitJSModuleInterop module;

    public LanguageViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public void TheViewAndThePaletteUseTheCurrentLanguage()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");
        try
        {
            var view = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, new Diagram(Schema)));
            var palette = Render<MusselDiagramPalette>(parameters => parameters.Add(p => p.View, view.Instance));

            Assert.Equal("最前面へ移動", view.Instance.Commands[DiagramCommands.BringToFront].DisplayName);
            Assert.Equal("次の問題", view.Instance.Commands[DiagramCommands.NextIssue].DisplayName);
            var schema = (JsonElement)module.Invocations["attach"][0].Arguments[4]!;
            Assert.Equal("ここにはつなぐ所がありません", schema.GetProperty("reasons").GetProperty("noConnectionPoint").GetString());
            Assert.Equal("検索", schema.GetProperty("texts").GetProperty("searchPlaceholder").GetString());
            Assert.Equal("見つかりません", schema.GetProperty("texts").GetProperty("noMatches").GetString());
            Assert.Equal("検索", palette.Find("input").GetAttribute("placeholder"));
            palette.Find("input").Input("nothing");
            Assert.Equal("見つかりません", palette.Find(".mussel-diagram-palette-empty").TextContent);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void RegisteredOptionsGiveTheTextsAndADiagramCanHaveItsOwn()
    {
        Services.AddMusselDiagram(static options => options.Cultures[string.Empty] = new DiagramTexts
        {
            Commands = new Dictionary<string, string> { [DiagramCommands.BringToFront] = "Raise" },
            Reasons = new Dictionary<DiagramReasonKind, string> { [DiagramReasonKind.NoConnectionPoint] = "Nowhere" },
            SearchPlaceholder = "Find"
        });
        var own = new MusselDiagramOptions
        {
            Cultures = { [string.Empty] = new DiagramTexts { Commands = new Dictionary<string, string> { [DiagramCommands.BringToFront] = "Up" } } }
        };

        var view = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, new Diagram(Schema)));
        var palette = Render<MusselDiagramPalette>(parameters => parameters.Add(p => p.View, view.Instance));
        var other = Render<MusselDiagramView>(parameters => parameters.Add(static p => p.Diagram, new Diagram(Schema) { Options = own }));

        Assert.Equal("Raise", view.Instance.Commands[DiagramCommands.BringToFront].DisplayName);
        var schema = (JsonElement)module.Invocations["attach"][0].Arguments[4]!;
        Assert.Equal("Nowhere", schema.GetProperty("reasons").GetProperty("noConnectionPoint").GetString());
        Assert.Equal("Find", palette.Find("input").GetAttribute("placeholder"));
        Assert.Equal("Up", other.Instance.Commands[DiagramCommands.BringToFront].DisplayName);
    }
}
