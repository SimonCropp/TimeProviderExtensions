using System.Runtime.CompilerServices;

namespace TimeScheduler;

/// <summary>
/// Represents a default implementation of a <see cref="ITimeScheduler"/>,
/// that simply wraps built-in types, static methods, and static properties
/// in .NET framework.
/// </summary>
/// <remarks>
/// Learn more at <see href="https://github.com/egil/TimeScheduler"/>.
/// </remarks>
public partial class DefaultScheduler : ITimeScheduler
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public Task Delay(TimeSpan delay)
        => Task.Delay(delay);

    /// <inheritdoc/>
    public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        => Task.Delay(delay, cancellationToken);

    /// <inheritdoc/>
    public PeriodicTimer PeriodicTimer(TimeSpan period)
        => new PeriodicTimerWrapper(period);

    /// <inheritdoc/>
    public Task WaitAsync(Task task, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(task);
        return task.WaitAsync(timeout);
    }

    /// <inheritdoc/>
    public Task WaitAsync(Task task, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        return task.WaitAsync(timeout, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TResult> WaitAsync<TResult>(Task<TResult> task, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(task);
        return task.WaitAsync(timeout);
    }

    /// <inheritdoc/>
    public Task<TResult> WaitAsync<TResult>(Task<TResult> task, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        return task.WaitAsync(timeout, cancellationToken);
    }
}
