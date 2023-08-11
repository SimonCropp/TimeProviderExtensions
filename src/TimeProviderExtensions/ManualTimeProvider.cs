using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace TimeProviderExtensions;

/// <summary>
/// Represents a synthetic time provider that can be used to enable deterministic behavior in tests.
/// </summary>
/// <remarks>
/// Learn more at <see href="https://github.com/egil/TimeProviderExtensions"/>.
/// </remarks>
[DebuggerDisplay("{ToString()}. Scheduled callback count: {ScheduledCallbackCount}")]
public class ManualTimeProvider : TimeProvider
{
    internal const uint MaxSupportedTimeout = 0xfffffffe;
    internal const uint UnsignedInfinite = unchecked((uint)-1);
    internal static readonly DateTimeOffset DefaultStartDateTime = new(2000, 1, 1, 0, 0, 0, 0, TimeSpan.Zero);

    private readonly List<ManualTimerScheduledCallback> callbacks = new();
    private DateTimeOffset utcNow;
    private TimeZoneInfo localTimeZone;
    private TimeSpan autoAdvanceAmount = TimeSpan.Zero;

    /// <summary>
    /// Gets the number of callbacks that are scheduled to be triggered in the future.
    /// </summary>
    internal int ScheduledCallbackCount => callbacks.Count;

    /// <summary>
    /// Gets the starting date and time for this provider.
    /// </summary>
    public DateTimeOffset Start { get; }

    /// <summary>
    /// Gets or sets the amount of time by which time advances whenever the clock is read via <see cref="GetUtcNow"/>.
    /// </summary>
    /// <remarks>
    /// Set to <see cref="TimeSpan.Zero"/> to disable auto advance. The default value is <see cref="TimeSpan.Zero"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value than <see cref="TimeSpan.Zero"/>.</exception>
    public TimeSpan AutoAdvanceAmount
    {
        get => autoAdvanceAmount;
        set
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(AutoAdvanceAmount), "Auto advance amount cannot be less than zero. ");
            }

            autoAdvanceAmount = value;
        }
    }

    /// <summary>
    /// Gets the amount by which the value from <see cref="GetTimestamp"/> increments per second.
    /// </summary>
    /// <remarks>
    /// This is fixed to the value of <see cref="TimeSpan.TicksPerSecond"/>.
    /// </remarks>
    public override long TimestampFrequency { get; } = TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => localTimeZone;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualTimeProvider"/> class.
    /// </summary>
    /// <remarks>
    /// This creates a provider whose time is initially set to midnight January 1st 2000 and
    /// with the local time zone set to <see cref="TimeZoneInfo.Utc"/>.
    /// The provider is set to not automatically advance time each time it is read.
    /// </remarks>
    public ManualTimeProvider()
        : this(DefaultStartDateTime, TimeZoneInfo.Utc)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualTimeProvider"/> class.
    /// </summary>
    /// <param name="startDateTime">The initial time and date reported by the provider.</param>
    /// <remarks>
    /// The local time zone set to <see cref="TimeZoneInfo.Utc"/>.
    /// The provider is set to not automatically advance time each time it is read.
    /// </remarks>
    public ManualTimeProvider(DateTimeOffset startDateTime)
        : this(startDateTime, TimeZoneInfo.Utc)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualTimeProvider"/> class.
    /// </summary>
    /// <param name="startDateTime">The initial time and date reported by the provider.</param>
    /// <param name="localTimeZone">Optional local time zone to use during testing. Defaults to <see cref="TimeZoneInfo.Utc"/>.</param>
    /// <remarks>
    /// The provider is set to not automatically advance time each time it is read.
    /// </remarks>
    public ManualTimeProvider(DateTimeOffset startDateTime, TimeZoneInfo localTimeZone)
    {
        utcNow = startDateTime;
        this.localTimeZone = localTimeZone;
        Start = startDateTime;
    }

    /// <summary>
    /// Gets the current high-frequency value designed to measure small time intervals with high accuracy in the timer mechanism.
    /// </summary>
    /// <returns>A long integer representing the high-frequency counter value of the underlying timer mechanism. </returns>
    /// <remarks>
    /// This implementation bases timestamp on <see cref="DateTimeOffset.UtcTicks"/>,
    /// since the progression of time is represented by the date and time returned from <see cref="GetUtcNow()" />.
    /// </remarks>
    public override long GetTimestamp() => utcNow.UtcTicks;

    /// <summary>
    /// Gets a <see cref="DateTimeOffset"/> value whose date and time are set to the current
    /// Coordinated Universal Time (UTC) date and time and whose offset is Zero,
    /// all according to this <see cref="ManualTimeProvider"/>'s notion of time.
    /// </summary>
    /// <remarks>
    /// If <see cref="AutoAdvanceAmount"/> is greater than <see cref="TimeSpan.Zero"/>, calling this
    /// method will move time forward by the amount specified by <see cref="AutoAdvanceAmount"/>.
    /// The <see cref="DateTimeOffset"/> returned from this method will reflect the time before
    /// the auto advance was applied, if any.
    /// </remarks>
    public override DateTimeOffset GetUtcNow()
    {
        DateTimeOffset result;

        lock (callbacks)
        {
            result = utcNow;
            Advance(AutoAdvanceAmount);
        }

        return result;
    }

    /// <summary>Creates a new <see cref="ITimer"/> instance, using <see cref="TimeSpan"/> values to measure time intervals.</summary>
    /// <param name="callback">
    /// A delegate representing a method to be executed when the timer fires. The method specified for callback should be reentrant,
    /// as it may be invoked simultaneously on two threads if the timer fires again before or while a previous callback is still being handled.
    /// </param>
    /// <param name="state">An object to be passed to the <paramref name="callback"/>. This may be null.</param>
    /// <param name="dueTime">The amount of time to delay before <paramref name="callback"/> is invoked. Specify <see cref="Timeout.InfiniteTimeSpan"/> to prevent the timer from starting. Specify <see cref="TimeSpan.Zero"/> to start the timer immediately.</param>
    /// <param name="period">The time interval between invocations of <paramref name="callback"/>. Specify <see cref="Timeout.InfiniteTimeSpan"/> to disable periodic signaling.</param>
    /// <returns>
    /// The newly created <see cref="ITimer"/> instance.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The number of milliseconds in the value of <paramref name="dueTime"/> or <paramref name="period"/> is negative and not equal to <see cref="Timeout.Infinite"/>, or is greater than <see cref="int.MaxValue"/>.</exception>
    /// <remarks>
    /// <para>
    /// The delegate specified by the callback parameter is invoked once after <paramref name="dueTime"/> elapses, and thereafter each time the <paramref name="period"/> time interval elapses.
    /// </para>
    /// <para>
    /// If <paramref name="dueTime"/> is zero, the callback is invoked immediately. If <paramref name="dueTime"/> is -1 milliseconds, <paramref name="callback"/> is not invoked; the timer is disabled,
    /// but can be re-enabled by calling the <see cref="ITimer.Change"/> method.
    /// </para>
    /// <para>
    /// If <paramref name="period"/> is 0 or -1 milliseconds and <paramref name="dueTime"/> is positive, <paramref name="callback"/> is invoked once; the periodic behavior of the timer is disabled,
    /// but can be re-enabled using the <see cref="ITimer.Change"/> method.
    /// </para>
    /// <para>
    /// The return <see cref="ITimer"/> instance will be implicitly rooted while the timer is still scheduled.
    /// </para>
    /// <para>
    /// <see cref="CreateTimer"/> captures the <see cref="ExecutionContext"/> and stores that with the <see cref="ITimer"/> for use in invoking <paramref name="callback"/>
    /// each time it's called. That capture can be suppressed with <see cref="ExecutionContext.SuppressFlow"/>.
    /// </para>
    /// <para>
    /// To move time forward for the returned <see cref="ITimer"/>, call <see cref="Advance(TimeSpan)"/> or <see cref="SetUtcNow(DateTimeOffset)"/> on this time provider.
    /// </para>
    /// </remarks>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var result = new ManualTimer(callback, state, this);
        result.Change(dueTime, period);
        return result;
    }

    /// <summary>
    /// Sets the local time zone.
    /// </summary>
    /// <param name="localTimeZone">The local time zone.</param>
    public void SetLocalTimeZone(TimeZoneInfo localTimeZone)
    {
        this.localTimeZone = localTimeZone;
    }

    /// <summary>
    /// Advances time by a specific amount.
    /// </summary>
    /// <param name="delta">The amount of time to advance the clock by.</param>
    /// <remarks>
    /// Advancing time affects the timers created from this provider, and all other operations that are directly or
    /// indirectly using this provider as a time source. Whereas when using <see cref="TimeProvider.System"/>, time
    /// marches forward automatically in hardware, for the manual time provider the application is responsible for
    /// doing this explicitly by calling this method.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="delta"/> is negative. Going back in time is not supported.</exception>
    public void Advance(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Going back in time is not supported. ");
        }

        if (delta == TimeSpan.Zero)
        {
            return;
        }

        SetUtcNow(utcNow + delta);
    }

    /// <summary>
    /// Sets the date and time returned by <see cref="GetUtcNow()"/> to <paramref name="value"/> and triggers any
    /// scheduled items that are waiting for time to be forwarded.
    /// </summary>
    /// <param name="value">The new UtcNow time.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="value"/> is less than the value returned by <see cref="GetUtcNow()"/>. Going back in time is not supported.</exception>
    public void SetUtcNow(DateTimeOffset value)
    {
        if (value < utcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"The new UtcNow must be greater than or equal to the curren time {utcNow}. Going back in time is not supported.");
        }

        lock (callbacks)
        {
            // Double check in case another thread already advanced time.
            if (value <= utcNow)
            {
                return;
            }

            while (utcNow <= value && TryGetNext(value) is ManualTimerScheduledCallback mtsc)
            {
                utcNow = mtsc.CallbackTime;
                mtsc.Timer.TimerElapsed();
            }

            utcNow = value;
        }

        ManualTimerScheduledCallback? TryGetNext(DateTimeOffset targetUtcNow)
        {
            if (callbacks.Count > 0 && callbacks[0].CallbackTime <= targetUtcNow)
            {
                var callback = callbacks[0];
                callbacks.RemoveAt(0);
                return callback;
            }

            return null;
        }
    }

    /// <summary>
    /// Returns a string representation this clock's current time.
    /// </summary>
    /// <returns>A string representing the clock's current time.</returns>
    public override string ToString() => utcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);

    private void ScheduleCallback(ManualTimer timer, TimeSpan waitTime)
    {
        lock (callbacks)
        {
            var timerCallback = new ManualTimerScheduledCallback(timer, utcNow + waitTime);
            var insertPosition = callbacks.FindIndex(x => x.CallbackTime > timerCallback.CallbackTime);

            if (insertPosition == -1)
            {
                callbacks.Add(timerCallback);
            }
            else
            {
                callbacks.Insert(insertPosition, timerCallback);
            }
        }
    }

    private void RemoveCallback(ManualTimer timer)
    {
        lock (callbacks)
        {
            var existingIndexOf = callbacks.FindIndex(0, x => ReferenceEquals(x.Timer, timer));
            if (existingIndexOf >= 0)
                callbacks.RemoveAt(existingIndexOf);
        }
    }

    private readonly struct ManualTimerScheduledCallback :
        IEqualityComparer<ManualTimerScheduledCallback>,
        IComparable<ManualTimerScheduledCallback>
    {
        public readonly ManualTimer Timer { get; }

        public readonly DateTimeOffset CallbackTime { get; }

        public ManualTimerScheduledCallback(ManualTimer timer, DateTimeOffset callbackTime)
        {
            Timer = timer;
            CallbackTime = callbackTime;
        }

        public readonly bool Equals(ManualTimerScheduledCallback x, ManualTimerScheduledCallback y)
            => ReferenceEquals(x.Timer, y.Timer);

        public readonly int GetHashCode(ManualTimerScheduledCallback obj)
            => Timer.GetHashCode();

        public readonly int CompareTo(ManualTimerScheduledCallback other)
            => Comparer<DateTimeOffset>.Default.Compare(CallbackTime, other.CallbackTime);
    }

    private sealed class ManualTimer : ITimer
    {
        private ManualTimeProvider? timeProvider;
        private bool isDisposed;
        private bool running;

        private TimeSpan currentDueTime;
        private TimeSpan currentPeriod;
        private object? state;
        private TimerCallback? callback;

        public ManualTimer(TimerCallback callback, object? state, ManualTimeProvider timeProvider)
        {
            this.timeProvider = timeProvider;
            this.callback = callback;
            this.state = state;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            ValidateTimeSpanRange(dueTime);
            ValidateTimeSpanRange(period);

            if (isDisposed || timeProvider is null)
            {
                return false;
            }

            if (running)
            {
                timeProvider.RemoveCallback(this);
            }

            currentDueTime = dueTime;
            currentPeriod = period;

            if (currentDueTime != Timeout.InfiniteTimeSpan)
            {
                ScheduleCallback(dueTime);
            }

            return true;
        }

        public void Dispose()
        {
            if (isDisposed || timeProvider is null)
            {
                return;
            }

            isDisposed = true;

            if (running)
            {
                timeProvider.RemoveCallback(this);
            }

            callback = null;
            state = null;
            timeProvider = null;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
#if NETSTANDARD2_0
            return default;
#else
            return ValueTask.CompletedTask;
#endif
        }

        internal void TimerElapsed()
        {
            if (isDisposed || timeProvider is null)
            {
                return;
            }

            running = false;

            callback?.Invoke(state);

            if (currentPeriod != Timeout.InfiniteTimeSpan && currentPeriod != TimeSpan.Zero)
            {
                ScheduleCallback(currentPeriod);
            }
        }

        private void ScheduleCallback(TimeSpan waitTime)
        {
            if (isDisposed || timeProvider is null)
            {
                return;
            }

            running = true;

            if (waitTime == TimeSpan.Zero)
            {
                TimerElapsed();
            }
            else
            {
                timeProvider.ScheduleCallback(this, waitTime);
            }
        }

        private static void ValidateTimeSpanRange(TimeSpan time, [CallerArgumentExpression("time")] string? parameter = null)
        {
            long tm = (long)time.TotalMilliseconds;
            if (tm < -1)
            {
                throw new ArgumentOutOfRangeException(parameter, $"{parameter}.TotalMilliseconds must be greater than -1.");
            }

            if (tm > MaxSupportedTimeout)
            {
                throw new ArgumentOutOfRangeException(parameter, $"{parameter}.TotalMilliseconds must be less than than {MaxSupportedTimeout}.");
            }
        }
    }
}
