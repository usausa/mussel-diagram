namespace MusselDiagram.Internal;

using Microsoft.JSInterop;

internal static class ScriptCalls
{
    public static async Task ReleaseAsync(IJSObjectReference? attached, IJSObjectReference? loaded)
    {
        try
        {
            if (attached is not null)
            {
                try
                {
                    await attached.InvokeVoidAsync("dispose").ConfigureAwait(true);
                }
                catch (JSException)
                {
                }

                await attached.DisposeAsync().ConfigureAwait(true);
            }

            if (loaded is not null)
            {
                await loaded.DisposeAsync().ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
