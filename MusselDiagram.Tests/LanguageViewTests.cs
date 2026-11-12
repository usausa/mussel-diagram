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

            Assert.Equal("最前面へ", view.Instance.Commands[DiagramCommands.BringToFront].DisplayName);
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
}
