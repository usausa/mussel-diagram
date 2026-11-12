namespace MusselDiagram;

using System.Collections.Immutable;

using Microsoft.AspNetCore.Components;

public sealed record DiagramTemplateContext(DiagramNode Element)
{
    public double Width => Element.Bounds.Width;

    public double Height => Element.Bounds.Height;

    public string? Label => Element.Label;

    public ImmutableDictionary<string, string> Texts => Element.Texts;

    public object? Data => Element.Data;
}

public sealed class DiagramTemplate : ComponentBase, IDisposable
{
    private MusselDiagramView? registeredView;

    [Parameter]
    [EditorRequired]
    public string Type { get; set; } = default!;

    [Parameter]
    public RenderFragment<DiagramTemplateContext>? ChildContent { get; set; }

    [CascadingParameter]
    private MusselDiagramView? View { get; set; }

    public void Dispose() => registeredView?.RemoveTemplate(this);

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(registeredView, View))
        {
            registeredView?.RemoveTemplate(this);
            registeredView = View;
        }

        registeredView?.SetTemplate(this);
    }

    protected override bool ShouldRender() => false;
}
