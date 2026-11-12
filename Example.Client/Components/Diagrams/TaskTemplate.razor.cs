namespace Example.Client.Components.Diagrams;

public sealed partial class TaskTemplate
{
    private static string? OwnerOf(DiagramTemplateContext item) =>
        item.Texts.GetValueOrDefault("owner") is { Length: > 0 } owner ? owner : null;

    private static string Owner(DiagramTemplateContext item) => OwnerOf(item) ?? "Owner";

    private static string OwnerClass(DiagramTemplateContext item) => OwnerOf(item) is null ? "task-owner task-empty" : "task-owner";
}
