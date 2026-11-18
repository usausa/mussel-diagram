namespace MusselDiagram;

using System.Collections.Immutable;

using MusselDiagram.Internal;

public sealed record DiagramLoadResult(DiagramSnapshot Snapshot, ImmutableArray<DiagramIssue> Issues)
{
    internal IReadOnlyList<PendingRoute> PendingRoutes { get; init; } = [];
}

public static class DiagramJson
{
    public static void Write(Stream stream, DiagramSnapshot snapshot, DiagramSchema schema) =>
        DiagramJsonFormat.Write(stream, snapshot, SchemaIndex.For(schema));

    public static Task WriteAsync(Stream stream, DiagramSnapshot snapshot, DiagramSchema schema, CancellationToken cancellationToken = default) =>
        DiagramJsonFormat.WriteAsync(stream, snapshot, SchemaIndex.For(schema), cancellationToken);

    public static DiagramLoadResult Read(Stream stream, DiagramSchema schema) =>
        Read(stream, schema, Diagram.CanDefer, Diagram.DefaultRouteLimit);

    public static async Task<DiagramLoadResult> ReadAsync(Stream stream, DiagramSchema schema, CancellationToken cancellationToken = default)
    {
        var defer = Diagram.CanDefer;
        return Read(await DiagramJsonFormat.ParseAsync(stream, cancellationToken).ConfigureAwait(false), schema, defer, Diagram.DefaultRouteLimit);
    }

    internal static DiagramLoadResult Read(Stream stream, DiagramSchema schema, bool defer, TimeSpan routeLimit) =>
        Read(DiagramJsonFormat.Parse(stream), schema, defer, routeLimit);

    private static DiagramLoadResult Read(DiagramJsonDocument document, DiagramSchema schema, bool defer, TimeSpan routeLimit) =>
        DiagramJsonFormat.Read(document, SchemaIndex.For(schema), 0, defer ? new RouteBudget(routeLimit) : null, waitForRoutes: !defer, skipAppRouter: defer);
}
