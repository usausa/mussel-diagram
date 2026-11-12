namespace MusselDiagram;

using System.Collections.Immutable;

public sealed record DiagramTexts
{
    public static DiagramTexts English { get; } = new()
    {
        Reasons = new Dictionary<DiagramReasonKind, string>
        {
            [DiagramReasonKind.NoConnectionPoint] = "There is no connection point here.",
            [DiagramReasonKind.Direction] = "The directions of the ends do not match.",
            [DiagramReasonKind.PortKind] = "The kinds of the ports differ.",
            [DiagramReasonKind.Permission] = "This element cannot be changed.",
            [DiagramReasonKind.Limit] = "The port cannot take more links.",
            [DiagramReasonKind.SelfOrDuplicate] = "The link would connect an element to itself or repeat a link.",
            [DiagramReasonKind.PeerType] = "The port does not accept this type.",
            [DiagramReasonKind.CrossParent] = "The ends are in different groups.",
            [DiagramReasonKind.Cycle] = "The link would make a cycle.",
            [DiagramReasonKind.Count] = "The diagram has the maximum number of this type.",
            [DiagramReasonKind.Nesting] = "The element cannot be placed here.",
            [DiagramReasonKind.Changed] = "The diagram has changed. Try again.",
            [DiagramReasonKind.Canceled] = "The change was canceled.",
            [DiagramReasonKind.TooLarge] = "The image is larger than the browser can draw."
        },
        Issues = new Dictionary<DiagramIssueKind, string>
        {
            [DiagramIssueKind.ConnectionRule] = "The link breaks a connection rule: {0}",
            [DiagramIssueKind.NestingRule] = "The element breaks a nesting rule: {0}",
            [DiagramIssueKind.MaxCount] = "There are more elements of the type '{0}' than {1}.",
            [DiagramIssueKind.RequiredType] = "The diagram needs an element of the type '{0}'.",
            [DiagramIssueKind.RequiredPort] = "The port '{0}' needs a link.",
            [DiagramIssueKind.MissingPort] = "The port '{0}' does not exist.",
            [DiagramIssueKind.MissingSharedItem] = "The shared item '{0}' does not exist.",
            [DiagramIssueKind.UnregisteredType] = "The type '{0}' is not registered.",
            [DiagramIssueKind.DuplicateId] = "The ID '{0}' is used more than once.",
            [DiagramIssueKind.MissingEnd] = "An end of the link does not exist.",
            [DiagramIssueKind.InvalidParent] = "The parent '{0}' does not exist or is not a tree group.",
            [DiagramIssueKind.InvalidMember] = "The member '{0}' does not exist or is not a node or a tree group.",
            [DiagramIssueKind.NoMembers] = "The overlay group has no members.",
            [DiagramIssueKind.KindMismatch] = "The kind of the group differs from the kind of the type '{0}'.",
            [DiagramIssueKind.InvalidSize] = "The size is not positive.",
            [DiagramIssueKind.UnreadableData] = "The data cannot be read as the data type of '{0}'.",
            [DiagramIssueKind.RemovedSvgContent] = "Removed from the icon '{0}': {1}",
            [DiagramIssueKind.RejectedUrl] = "The icon '{0}' refers to URLs that were not used: {1}",
            [DiagramIssueKind.UnreadableIcon] = "The icon '{0}' is not a readable SVG.",
            [DiagramIssueKind.ImageNotEmbedded] = "The image '{0}' could not be embedded."
        },
        Commands = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Undo"] = "Undo",
            ["Redo"] = "Redo",
            ["Delete"] = "Delete",
            ["Cut"] = "Cut",
            ["Copy"] = "Copy",
            ["Paste"] = "Paste",
            ["SelectAll"] = "Select all",
            ["ClearSelection"] = "Clear selection",
            ["EditText"] = "Edit text",
            ["QuickAdd"] = "Quick add",
            ["Group"] = "Group",
            ["Ungroup"] = "Ungroup",
            ["CreateOverlayGroup"] = "Create overlay group",
            ["AddMembers"] = "Add to group",
            ["RemoveMembers"] = "Remove from group",
            ["BringToFront"] = "Bring to front",
            ["SendToBack"] = "Send to back",
            ["BringForward"] = "Bring forward",
            ["SendBackward"] = "Send backward",
            ["AlignLeft"] = "Align left",
            ["AlignCenter"] = "Align center",
            ["AlignRight"] = "Align right",
            ["AlignTop"] = "Align top",
            ["AlignMiddle"] = "Align middle",
            ["AlignBottom"] = "Align bottom",
            ["DistributeHorizontally"] = "Distribute horizontally",
            ["DistributeVertically"] = "Distribute vertically",
            ["ToggleLock"] = "Lock or unlock",
            ["ZoomIn"] = "Zoom in",
            ["ZoomOut"] = "Zoom out",
            ["ResetZoom"] = "Zoom to 100%",
            ["ZoomToFit"] = "Fit to view",
            ["ZoomToSelection"] = "Zoom to selection",
            ["NextIssue"] = "Next issue",
            ["PreviousIssue"] = "Previous issue"
        },
        SearchPlaceholder = "Search",
        NoMatches = "No matches"
    };

    public IReadOnlyDictionary<DiagramReasonKind, string> Reasons { get; init; } = ImmutableDictionary<DiagramReasonKind, string>.Empty;

    public IReadOnlyDictionary<DiagramIssueKind, string> Issues { get; init; } = ImmutableDictionary<DiagramIssueKind, string>.Empty;

    public IReadOnlyDictionary<string, string> Commands { get; init; } = ImmutableDictionary<string, string>.Empty;

    public string? SearchPlaceholder { get; init; }

    public string? NoMatches { get; init; }
}
