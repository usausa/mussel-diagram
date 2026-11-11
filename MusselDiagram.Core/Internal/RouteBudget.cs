namespace MusselDiagram.Internal;

using System.Diagnostics;

internal sealed class RouteBudget(TimeSpan limit)
{
    private readonly long deadline = Stopwatch.GetTimestamp() + (long)(limit.TotalSeconds * Stopwatch.Frequency);

    public bool Exceeded => Stopwatch.GetTimestamp() > deadline;
}
