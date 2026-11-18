namespace MusselDiagram;

using Microsoft.JSInterop;

internal sealed class LateRuntime : IJSRuntime
{
    public TaskCompletionSource<IJSObjectReference> Import { get; } = new();

    public LateModule Module { get; } = new();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        identifier == "import" ? new ValueTask<TValue>((Task<TValue>)(object)Import.Task) : default;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
}

internal sealed class LateModule : IJSObjectReference
{
    public List<string> Calls { get; } = [];

    public List<object?[]?> Arguments { get; } = [];

    public string? Hold { get; set; }

    public string? Fail { get; set; }

    public LateModule? Returned { get; set; }

    public TaskCompletionSource Resume { get; } = new();

    public bool Released { get; private set; }

    public ValueTask DisposeAsync()
    {
        Released = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        ObjectDisposedException.ThrowIf(Released, this);
        Calls.Add(identifier);
        Arguments.Add(args);
        if (identifier == Fail)
        {
            return ValueTask.FromException<TValue>(new JSException("The call failed."));
        }

        if (identifier == Hold)
        {
            return new ValueTask<TValue>(Resume.Task.ContinueWith(_ => Result<TValue>(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default));
        }

        return new ValueTask<TValue>(Result<TValue>());
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);

    private TValue Result<TValue>() => typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)(Returned ?? this) : default!;
}
