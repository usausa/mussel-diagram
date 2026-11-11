namespace MusselDiagram;

public sealed class MusselDiagramViewTests : BunitContext
{
    private readonly BunitJSModuleInterop module;

    public MusselDiagramViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        module = JSInterop.SetupModule(MusselDiagramView.ModulePath);
    }

    [Fact]
    public void ViewDrawsTheDiagramAndTheInteractionLayerAndAttachesTheScript()
    {
        var cut = Render<MusselDiagramView>(static parameters => parameters
            .Add(static p => p.Diagram, new Diagram(new DiagramSchema()))
            .AddUnmatched("class", "app-view"));

        Assert.Contains("app-view", cut.Find(".mussel-diagram").ClassList);
        Assert.Single(cut.FindAll(".mussel-diagram-stage > svg.mussel-diagram-canvas"));
        Assert.Single(cut.FindAll(".mussel-diagram-stage > svg.mussel-diagram-overlay"));
        module.VerifyInvoke("attach");
    }
}
