#if NET6_0_OR_GREATER
#if TargetMicrosoftTestTimeProvider && !RELEASE
using SutTimeProvider = Microsoft.Extensions.Time.Testing.FakeTimeProvider;
#else
using SutTimeProvider = TimeProviderExtensions.ManualTimeProvider;
#endif

namespace TimeProviderExtensions;

public class ManualTimeProviderPeriodicTimerTests
{
    [Fact]
    public void PeriodicTimer_WaitForNextTickAsync_cancelled_immediately()
    {
        using var cts = new CancellationTokenSource();
        var sut = new SutTimeProvider();
        using var periodicTimer = sut.CreatePeriodicTimer(TimeSpan.FromMilliseconds(1));

        cts.Cancel();
        var task = periodicTimer.WaitForNextTickAsync(cts.Token);

        task.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task PeriodicTimer_WaitForNextTickAsync_complete_immediately()
    {
        var sut = new SutTimeProvider();
        using var periodicTimer = sut.CreatePeriodicTimer(TimeSpan.FromMilliseconds(1));

        sut.Advance(TimeSpan.FromMilliseconds(1));
        var task = periodicTimer.WaitForNextTickAsync();

        (await task).Should().BeTrue();
    }

    [Fact]
    public async Task PeriodicTimer_WaitForNextTickAsync_completes()
    {
        var startTime = DateTimeOffset.UtcNow;
        var future = TimeSpan.FromMilliseconds(1);
        var sut = new SutTimeProvider(startTime);
        using var periodicTimer = sut.CreatePeriodicTimer(TimeSpan.FromMilliseconds(1));
        var task = periodicTimer.WaitForNextTickAsync();

        sut.Advance(future);

        (await task).Should().BeTrue();
    }

    [Fact]
    public async Task PeriodicTimer_WaitForNextTickAsync_completes_after_dispose()
    {
        var startTime = DateTimeOffset.UtcNow;
        var sut = new SutTimeProvider(startTime);
        var periodicTimer = sut.CreatePeriodicTimer(TimeSpan.FromMilliseconds(1));
        var task = periodicTimer.WaitForNextTickAsync();

        periodicTimer.Dispose();

        (await task).Should().BeFalse();
    }

    [Fact]
    public async Task PeriodicTimer_WaitForNextTickAsync_cancelled_with_exception()
    {
        using var cts = new CancellationTokenSource();
        var sut = new SutTimeProvider();
        using var periodicTimer = sut.CreatePeriodicTimer(TimeSpan.FromMilliseconds(1));
        var task = periodicTimer.WaitForNextTickAsync(cts.Token);
        cts.Cancel();

        var throws = async () => await task;

        await throws
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void PeriodicTimer_WaitForNextTickAsync_twice_throws()
    {
        var sut = new SutTimeProvider();
        using var periodicTimer = sut.CreatePeriodicTimer(TimeSpan.FromMilliseconds(1));

        _ = periodicTimer.WaitForNextTickAsync();
        var throws = () => periodicTimer.WaitForNextTickAsync();

        throws.Should().ThrowExactly<InvalidOperationException>();
    }

    [Fact]
    public void PeriodicTimer_WaitForNextTickAsync_completes_multiple()
    {
        var sut = new SutTimeProvider();
        var calledTimes = 0;
        var interval = TimeSpan.FromSeconds(1);
        var looper = WaitForNextTickInLoop(sut, () => calledTimes++, interval);

        sut.Advance(interval);
        calledTimes.Should().Be(1);

        sut.Advance(interval);
        calledTimes.Should().Be(2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PeriodicTimer_WaitForNextTickAsync_completes_iterations(int expectedCallbacks)
    {
        var sut = new SutTimeProvider();
        var calledTimes = 0;
        var interval = TimeSpan.FromSeconds(1);
        var looper = WaitForNextTickInLoop(sut, () => calledTimes++, interval);

        sut.Advance(interval * expectedCallbacks);

        calledTimes.Should().Be(expectedCallbacks);
    }

    [Fact]
    public async void PeriodicTimer_WaitForNextTickAsync_exists_on_timer_Dispose()
    {
        var sut = new SutTimeProvider();
        var periodicTimer = sut.CreatePeriodicTimer(TimeSpan.FromSeconds(1));
        var disposeTask = WaitForNextTickToReturnFalse(periodicTimer);
        sut.Advance(TimeSpan.FromSeconds(1));

        periodicTimer.Dispose();

        (await disposeTask).Should().BeFalse();

#if NET6_0_OR_GREATER && !NET8_0_OR_GREATER
        static async Task<bool> WaitForNextTickToReturnFalse(PeriodicTimerWrapper periodicTimer)
#else
        static async Task<bool> WaitForNextTickToReturnFalse(System.Threading.PeriodicTimer periodicTimer)
#endif
        {
            while (await periodicTimer.WaitForNextTickAsync(CancellationToken.None))
            {
            }

            return false;
        }
    }

    [Fact]
    public void GetUtcNow_matches_time_when_WaitForNextTickAsync_is_invoked()
    {
        var sut = new SutTimeProvider();
        var startTime = sut.GetUtcNow();
        var callbackTimes = new List<DateTimeOffset>();
        var interval = TimeSpan.FromSeconds(5);
        var looper = WaitForNextTickInLoop(sut, () => callbackTimes.Add(sut.GetUtcNow()), interval);

        sut.Advance(interval * 3);

        callbackTimes.Should().Equal(
            startTime + interval * 1,
            startTime + interval * 2,
            startTime + interval * 3);
    }

    [Fact]
    public async void Cancelling_token_after_WaitForNextTickAsync_safe()
    {
        var sut = new SutTimeProvider();
        var interval = TimeSpan.FromSeconds(3);
        using var cts = new CancellationTokenSource();
        var periodicTimer = sut.CreatePeriodicTimer(interval);
        var cleanCancelTask = CancelAfterWaitForNextTick(periodicTimer, cts);

        sut.Advance(interval);

        await cleanCancelTask;
#if NET6_0_OR_GREATER && !NET8_0_OR_GREATER
        static async Task CancelAfterWaitForNextTick(PeriodicTimerWrapper periodicTimer, CancellationTokenSource cts)
#else
        static async Task CancelAfterWaitForNextTick(System.Threading.PeriodicTimer periodicTimer, CancellationTokenSource cts)
#endif
        {
            while (await periodicTimer.WaitForNextTickAsync(cts.Token))
            {
                break;
            }
            cts.Cancel();
        }
    }

    private static async Task WaitForNextTickInLoop(TimeProvider scheduler, Action callback, TimeSpan interval)
    {
        using var periodicTimer = scheduler.CreatePeriodicTimer(interval);
        while (await periodicTimer.WaitForNextTickAsync(CancellationToken.None).ConfigureAwait(false))
        {
            callback();
        }
    }
}
#endif