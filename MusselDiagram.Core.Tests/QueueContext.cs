namespace MusselDiagram;

internal sealed class QueueContext : SynchronizationContext
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    private readonly Queue<(SendOrPostCallback Callback, object? State)> queue = new();

    private readonly object gate = new();

    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (gate)
        {
            queue.Enqueue((d, state));
            Monitor.PulseAll(gate);
        }
    }

    public void RunOne()
    {
        (SendOrPostCallback Callback, object? State) item;
        lock (gate)
        {
            while (queue.Count == 0)
            {
                if (!Monitor.Wait(gate, Limit))
                {
                    throw new TimeoutException();
                }
            }

            item = queue.Dequeue();
        }

        item.Callback(item.State);
    }

    public void RunUntil(Task task)
    {
        var deadline = DateTime.UtcNow + Limit;
        while (!task.IsCompleted)
        {
            (SendOrPostCallback Callback, object? State) item;
            lock (gate)
            {
                if (queue.Count == 0)
                {
                    if (DateTime.UtcNow > deadline)
                    {
                        throw new TimeoutException();
                    }

                    Monitor.Wait(gate, TimeSpan.FromMilliseconds(10));
                    continue;
                }

                item = queue.Dequeue();
            }

            item.Callback(item.State);
        }
    }
}
