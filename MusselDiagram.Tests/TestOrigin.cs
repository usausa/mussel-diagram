namespace MusselDiagram;

using System.Text.Json;

using MusselDiagram.Internal;

internal static class TestOrigin
{
    public static ScriptOrigin Of(DiagramSnapshot snapshot, string? text = null) =>
        ScriptOrigin.Read(JsonSerializer.SerializeToElement(new
        {
            boxes = snapshot.Nodes.Cast<DiagramBox>().Concat(snapshot.Groups).Select(static x => new object?[] { x.Id, x.Bounds.X, x.Bounds.Y, x.Bounds.Width, x.Bounds.Height, x.ParentId }),
            links = snapshot.Links.Select(static x => new object?[] { x.Id, x.Source.ElementId, x.Source.PortId, x.Target.ElementId, x.Target.PortId }),
            text
        }));
}
