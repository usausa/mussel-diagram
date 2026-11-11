namespace MusselDiagram.Internal;

internal static class BuiltInTexts
{
    public static string? Reason(DiagramReasonKind kind) => kind switch
    {
        DiagramReasonKind.Changed => "The diagram has changed. Try again.",
        DiagramReasonKind.Permission => "This element cannot be changed.",
        DiagramReasonKind.Canceled => "The change was canceled.",
        _ => null
    };
}
