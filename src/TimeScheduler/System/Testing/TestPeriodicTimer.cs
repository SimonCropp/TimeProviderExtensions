namespace System.Testing;

internal sealed class TestPeriodicTimer : TimeScheduler.PeriodicTimer
{
    private readonly ITimer timer;
    private bool stopped;
    private TaskCompletionSource<bool>? completionSource;
    private CancellationTokenRegistration? cancellationRegistration;

    public TestPeriodicTimer(TimeSpan period, TimeProvider timeProvider)
    {
        timer = timeProvider.CreateTimer(Signal, null, period, period);
    }

    public override ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
    {
        if (completionSource is not null && !completionSource.Task.IsCompleted)
        {
            throw new InvalidOperationException("WaitForNextTickAsync should only be used by one consumer at a time. Failing to do so is an error.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<bool>(cancellationToken);
        }

        if (!stopped && completionSource is not null)
        {
            return new ValueTask<bool>(completionSource.Task);
        }

        completionSource = new();
        cancellationRegistration?.Unregister();
        cancellationRegistration = cancellationToken.Register(() =>
        {
            completionSource.TrySetCanceled(cancellationToken);
        });

        return new ValueTask<bool>(completionSource.Task);
    }

    private void Signal(object? state)
    {
        if (completionSource is TaskCompletionSource<bool> tcs)
        {
            completionSource = null;
            tcs.TrySetResult(!stopped);
        }
        else
        {
            completionSource = new();
            completionSource.SetResult(!stopped);
        }
    }

    protected override void Dispose(bool disposing)
    {
        stopped = true;
        Signal(null);
        cancellationRegistration?.Unregister();
        timer.Dispose();
        base.Dispose(disposing);
    }
}
