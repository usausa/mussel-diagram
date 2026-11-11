namespace MusselDiagram;

using System.Collections.Immutable;

public interface IDiagramRouter
{
    ValueTask<DiagramRouteResult> RouteAsync(DiagramRouteRequest request, CancellationToken cancellationToken);
}

public enum DiagramLinkEndSide
{
    Source,
    Target
}

public sealed record DiagramRouteEnd
{
    public required DiagramPoint Position { get; init; }

    public required DiagramSide Direction { get; init; }
}

public sealed record DiagramRouteRequest
{
    public required DiagramRouteEnd Source { get; init; }

    public required DiagramRouteEnd Target { get; init; }

    public required ImmutableArray<DiagramRect> Obstacles { get; init; }

    public DiagramLinkType? LinkType { get; init; }
}

public sealed record DiagramRouteResult
{
    public ImmutableArray<DiagramPoint> Points { get; init; } = [];

    public string? Error { get; init; }
}

public sealed record DiagramLinkGeometry
{
    public required ImmutableArray<DiagramPoint> Points { get; init; }

    public required DiagramPoint SourcePoint { get; init; }

    public required DiagramPoint TargetPoint { get; init; }

    public required double SourceAngle { get; init; }

    public required double TargetAngle { get; init; }

    public required string Path { get; init; }
}
