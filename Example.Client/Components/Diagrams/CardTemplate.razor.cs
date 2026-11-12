namespace Example.Client.Components.Diagrams;

public sealed partial class CardTemplate
{
    private static string? NoteOf(DiagramTemplateContext item) =>
        item.Texts.GetValueOrDefault("note") is { Length: > 0 } note ? note : null;

    private static string Note(DiagramTemplateContext item) => NoteOf(item) ?? "補足";

    private static string NoteClass(DiagramTemplateContext item) => NoteOf(item) is null ? "card-note card-empty" : "card-note";
}
