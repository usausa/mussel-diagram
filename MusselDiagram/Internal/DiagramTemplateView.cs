namespace MusselDiagram.Internal;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

internal sealed class DiagramTemplateView : ComponentBase
{
    private DiagramNode? renderedNode;

    private DiagramTemplate? renderedTemplate;

    private int renderedVersion = -1;

    [Parameter]
    [EditorRequired]
    public DiagramNode Node { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public DiagramTemplate Template { get; set; } = default!;

    [Parameter]
    public int Version { get; set; }

    protected override bool ShouldRender() =>
        !ReferenceEquals(Node, renderedNode) || !ReferenceEquals(Template, renderedTemplate) || (Version != renderedVersion);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        renderedNode = Node;
        renderedTemplate = Template;
        renderedVersion = Version;
        if (Template.ChildContent is { } content)
        {
            builder.AddContent(0, content(new DiagramTemplateContext(Node)));
        }
    }
}
