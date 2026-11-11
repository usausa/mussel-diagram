namespace MusselDiagram;

using System.Collections.Immutable;

using MusselDiagram.Internal;

public abstract record DiagramElement
{
    public string Id { get; init; } = DiagramIds.Create();

    public required string Type { get; init; }

    public object? Data { get; init; }
}

public abstract record DiagramBox : DiagramElement
{
    public DiagramRect Bounds { get; init; }

    public string? ParentId { get; init; }

    public string? Label { get; init; }

    public ImmutableDictionary<string, string> Texts { get; init; } = [];

    public string? Icon { get; init; }

    public bool Locked { get; init; }

    public DiagramPermissions? Permissions { get; init; }
}

public sealed record DiagramNode : DiagramBox;

public sealed record DiagramGroup : DiagramBox
{
    public DiagramGroupKind Kind { get; init; }

    public ImmutableArray<string> MemberIds { get; init; } = [];
}

public readonly record struct DiagramLinkEnd(string ElementId, string? PortId = null);

public sealed record DiagramLink : DiagramElement
{
    public required DiagramLinkEnd Source { get; init; }

    public required DiagramLinkEnd Target { get; init; }

    public ImmutableArray<DiagramPoint> Route { get; init; } = [];

    public DiagramRouteState RouteState { get; init; }

    public string? RouteError { get; init; }

    public bool Locked { get; init; }

    public DiagramPermissions? Permissions { get; init; }
}
