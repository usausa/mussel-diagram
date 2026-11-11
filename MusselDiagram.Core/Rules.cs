namespace MusselDiagram;

using System.Collections.Immutable;

public sealed record DiagramAppRules
{
    public Func<DiagramConnectionContext, DiagramRuleResult>? CanConnect { get; init; }

    public Func<DiagramNestingContext, DiagramRuleResult>? CanNest { get; init; }

    public Func<DiagramDeletionContext, DiagramRuleResult>? CanDelete { get; init; }

    public Func<DiagramPaletteContext, bool>? InPalette { get; init; }

    public Func<DiagramSnapshot, IEnumerable<DiagramIssue>>? Check { get; init; }
}

public readonly record struct DiagramRuleResult
{
    private DiagramRuleResult(bool allowed, string? reason)
    {
        Allowed = allowed;
        Reason = reason;
    }

    public static DiagramRuleResult Allow { get; } = new(true, null);

    public bool Allowed { get; }

    public string? Reason { get; }

    public static DiagramRuleResult Reject(string reason) => new(false, reason);
}

public sealed record DiagramConnectionContext
{
    public required DiagramSnapshot Snapshot { get; init; }

    public required DiagramLinkEnd Source { get; init; }

    public required DiagramLinkEnd Target { get; init; }

    public required string LinkType { get; init; }

    public string? Reconnecting { get; init; }
}

public sealed record DiagramNestingContext
{
    public required DiagramSnapshot Snapshot { get; init; }

    public string? ElementId { get; init; }

    public required string Type { get; init; }

    public string? ParentId { get; init; }
}

public sealed record DiagramDeletionContext
{
    public required DiagramSnapshot Snapshot { get; init; }

    public required ImmutableArray<string> ElementIds { get; init; }
}

public sealed record DiagramPaletteContext
{
    public required DiagramSnapshot Snapshot { get; init; }

    public required string Type { get; init; }
}

public enum DiagramIssueSeverity
{
    Error,
    Warning
}

public enum DiagramIssueKind
{
    ConnectionRule,
    NestingRule,
    MaxCount,
    RequiredType,
    RequiredPort,
    MissingPort,
    MissingSharedItem,
    UnregisteredType,
    App,
    DuplicateId,
    MissingEnd,
    InvalidParent,
    InvalidMember,
    KindMismatch,
    InvalidSize,
    UnreadableData,
    RemovedSvgContent,
    RejectedUrl,
    ImageNotEmbedded
}

public sealed record DiagramIssue(DiagramIssueSeverity Severity, DiagramIssueKind Kind, string? Message)
{
    public ImmutableArray<string> ElementIds { get; init; } = [];

    public string? PortId { get; init; }

    public string? Slot { get; init; }

    public ImmutableArray<string> Arguments { get; init; } = [];
}
