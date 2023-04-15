namespace System.Testing;

public class TestTimeProviderWaitAsyncTests
{
    internal const uint MaxSupportedTimeout = 0xfffffffe;
    private readonly static TimeSpan DelayedTaskDelay = TimeSpan.FromMilliseconds(2);
    private readonly static string StringTaskResult = Guid.NewGuid().ToString();

    private static async Task DelayedTask(TimeProvider scheduler)
    {
        await scheduler.Delay(DelayedTaskDelay);
    }

    private static async Task<string> DelayedStringTask(TimeProvider scheduler)
    {
        await scheduler.Delay(DelayedTaskDelay);
        return StringTaskResult;
    }

    public static TheoryData<Func<TestTimeProvider, Task>> NullTaskInvalidInvocations { get; } =
        new TheoryData<Func<TestTimeProvider, Task>>
        {
            sut => default(Task)!.WaitAsync(TimeSpan.FromSeconds(1), sut),
            sut => default(Task)!.WaitAsync(TimeSpan.FromSeconds(1), sut, CancellationToken.None),
            sut => default(Task<string>)!.WaitAsync(TimeSpan.FromSeconds(1), sut),
            sut => default(Task<string>)!.WaitAsync(TimeSpan.FromSeconds(1), sut, CancellationToken.None),
        };

    [Theory, MemberData(nameof(NullTaskInvalidInvocations))]
    public async Task WaitAsync_task_input_validation(Func<TestTimeProvider, Task> invalidInvocation)
    {
        using var sut = new TestTimeProvider();

        await sut.Invoking(invalidInvocation)
            .Should()
            .ThrowAsync<ArgumentNullException>()
            .WithParameterName("task");
    }

    public static TheoryData<Func<TestTimeProvider, Task>> TimeoutInvalidInvocations { get; } =
        new TheoryData<Func<TestTimeProvider, Task>>
        {
            sut => DelayedTask(sut).WaitAsync(TimeSpan.FromMilliseconds(-2), sut),
            sut => DelayedTask(sut).WaitAsync(TimeSpan.FromMilliseconds(-2), sut, CancellationToken.None),
            sut => DelayedStringTask(sut).WaitAsync(TimeSpan.FromMilliseconds(-2), sut),
            sut => DelayedStringTask(sut).WaitAsync(TimeSpan.FromMilliseconds(-2), sut, CancellationToken.None),
            sut => DelayedTask(sut).WaitAsync(TimeSpan.FromMilliseconds(MaxSupportedTimeout + 1), sut),
            sut => DelayedTask(sut).WaitAsync(TimeSpan.FromMilliseconds(MaxSupportedTimeout + 1), sut, CancellationToken.None),
            sut => DelayedStringTask(sut).WaitAsync(TimeSpan.FromMilliseconds(MaxSupportedTimeout + 1), sut),
            sut => DelayedStringTask(sut).WaitAsync(TimeSpan.FromMilliseconds(MaxSupportedTimeout + 1), sut, CancellationToken.None),
        };

    [Theory, MemberData(nameof(TimeoutInvalidInvocations))]
    public async Task WaitAsync_timeout_input_validation(Func<TestTimeProvider, Task> invalidInvocation)
    {
        using var sut = new TestTimeProvider();

        await sut.Invoking(invalidInvocation)
            .Should()
            .ThrowAsync<ArgumentOutOfRangeException>();
        // This should be timeout in all cases, but it seems the .NET 8 implementation sometimes returns dueTime.
        //.WithParameterName("timeout"); 
    }

    public static TheoryData<Func<TestTimeProvider, Task>> CompletedInvocations { get; } =
        new TheoryData<Func<TestTimeProvider, Task>>
        {
            sut => Task.CompletedTask.WaitAsync(TimeSpan.FromMilliseconds(1), sut),
            sut => Task.CompletedTask.WaitAsync(TimeSpan.FromMilliseconds(1), sut, CancellationToken.None),
            sut => Task<string>.CompletedTask.WaitAsync(TimeSpan.FromMilliseconds(1), sut),
            sut => Task<string>.CompletedTask.WaitAsync(TimeSpan.FromMilliseconds(1), sut, CancellationToken.None),
        };

    [Theory, MemberData(nameof(CompletedInvocations))]
    public void WaitAsync_completes_immediately_when_task_is_completed(Func<TestTimeProvider, Task> completedInvocation)
    {
        using var sut = new TestTimeProvider();

        var task = completedInvocation(sut);

        task.Should().CompletedSuccessfully();
    }

    public static TheoryData<Func<TestTimeProvider, Task>> ImmediatelyCanceledInvocations { get; } =
        new TheoryData<Func<TestTimeProvider, Task>>
        {
            sut => DelayedTask(sut).WaitAsync(TimeSpan.FromMilliseconds(1), sut,new CancellationToken(canceled: true)),
            sut => DelayedStringTask(sut).WaitAsync(TimeSpan.FromMilliseconds(1), sut,new CancellationToken(canceled: true)),
        };

    [Theory, MemberData(nameof(ImmediatelyCanceledInvocations))]
    public void WaitAsync_canceled_immediately_when_cancellationToken_is_set(Func<TestTimeProvider, Task> canceledInvocation)
    {
        using var sut = new TestTimeProvider();

        var task = canceledInvocation(sut);

        task.Should().Canceled();
    }

    public static TheoryData<Func<TestTimeProvider, Task>> ImmediateTimedoutInvocations { get; } =
        new TheoryData<Func<TestTimeProvider, Task>>
        {
            sut => DelayedTask(sut).WaitAsync(TimeSpan.Zero, sut),
            sut => DelayedTask(sut).WaitAsync(TimeSpan.Zero, sut, CancellationToken.None),
            sut => DelayedStringTask(sut).WaitAsync(TimeSpan.Zero, sut),
            sut => DelayedStringTask(sut).WaitAsync(TimeSpan.Zero, sut, CancellationToken.None),
        };

    [Theory, MemberData(nameof(ImmediateTimedoutInvocations))]
    public async Task WaitAsync_throws_immediately_when_timeout_is_zero(Func<TestTimeProvider, Task> immediateTimedoutInvocation)
    {
        using var sut = new TestTimeProvider();

        await sut.Invoking(immediateTimedoutInvocation)
            .Should()
            .ThrowExactlyAsync<TimeoutException>();
    }

    public static TheoryData<Func<TestTimeProvider, Task>> ValidInvocations { get; } =
        new TheoryData<Func<TestTimeProvider, Task>>
        {
            sut => DelayedTask(sut).WaitAsync(TimeSpan.FromSeconds(1), sut),
            sut => DelayedTask(sut).WaitAsync(TimeSpan.FromSeconds(1), sut, CancellationToken.None),
            sut => DelayedStringTask(sut).WaitAsync(TimeSpan.FromSeconds(1), sut),
            sut => DelayedStringTask(sut).WaitAsync(TimeSpan.FromSeconds(1), sut, CancellationToken.None),
        };

    [Theory, MemberData(nameof(ValidInvocations))]
    public async Task WaitAsync_completes_successfully(Func<TestTimeProvider, Task> validInvocation)
    {
        using var sut = new TestTimeProvider();
        var task = validInvocation(sut);

        sut.ForwardTime(DelayedTaskDelay);

        await task
            .Should()
            .CompletedSuccessfullyAsync();
    }

    public static TheoryData<Func<TestTimeProvider, Task<string>>> ValidStringInvocations { get; } =
        new TheoryData<Func<TestTimeProvider, Task<string>>>
        {
            sut => DelayedStringTask(sut).WaitAsync(TimeSpan.FromSeconds(1), sut),
            sut => DelayedStringTask(sut).WaitAsync(TimeSpan.FromSeconds(1), sut, CancellationToken.None),
        };

    [Theory, MemberData(nameof(ValidStringInvocations))]
    public async Task WaitAsync_of_T_completes_successfully(Func<TestTimeProvider, Task<string>> validInvocation)
    {
        using var sut = new TestTimeProvider();
        var task = validInvocation(sut);

        sut.ForwardTime(DelayedTaskDelay);

        await task
            .Should()
            .CompletedSuccessfullyAsync();
        task.Result.Should().Be(StringTaskResult);
    }

    public static TheoryData<Func<TestTimeProvider, TimeSpan, Task>> TimedoutInvocations { get; } =
        new TheoryData<Func<TestTimeProvider, TimeSpan, Task>>
        {
            (sut, timeout) => DelayedTask(sut).WaitAsync(timeout, sut),
            (sut, timeout) => DelayedTask(sut).WaitAsync(timeout, sut, CancellationToken.None),
            (sut, timeout) => DelayedStringTask(sut).WaitAsync(timeout, sut),
            (sut, timeout) => DelayedStringTask(sut).WaitAsync(timeout, sut, CancellationToken.None),
        };

    [Theory, MemberData(nameof(TimedoutInvocations))]
    public async Task WaitAsync_throws_when_timeout_is_reached(Func<TestTimeProvider, TimeSpan, Task> invocationWithTime)
    {
        using var sut = new TestTimeProvider();
        var task = invocationWithTime(sut, TimeSpan.FromMilliseconds(1));

        sut.ForwardTime(TimeSpan.FromMilliseconds(1));

        await task.Awaiting(x => x)
            .Should()
            .ThrowExactlyAsync<TimeoutException>();
    }

    public static TheoryData<Func<TestTimeProvider, CancellationToken, Task>> CancelledInvocations { get; } =
        new TheoryData<Func<TestTimeProvider, CancellationToken, Task>>
        {
            (sut, token) => DelayedTask(sut).WaitAsync(TimeSpan.FromMilliseconds(1), sut, token),
            (sut, token) => DelayedStringTask(sut).WaitAsync(TimeSpan.FromMilliseconds(1), sut, token),
        };

    [Theory, MemberData(nameof(CancelledInvocations))]
    public async Task WaitAsync_throws_when_token_is_canceled(Func<TestTimeProvider, CancellationToken, Task> invocationWithCancelToken)
    {
        using var cts = new CancellationTokenSource();
        using var sut = new TestTimeProvider();
        var task = invocationWithCancelToken(sut, cts.Token);

        cts.Cancel();

        await task.Awaiting(x => x)
            .Should()
            .ThrowExactlyAsync<TaskCanceledException>();
    }
}
