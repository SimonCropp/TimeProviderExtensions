# TimeProvider Extensions

Extensions for [`System.TimeProvider`](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider) API. It includes a version of the `TimeProvider` type, named `ManualTimeProvider`, that allows you to control the progress of time during testing deterministically.

An instance of `TimeProvider` for production use is available on the `TimeProvider.System` property, and `ManualTimeProvider` can be used during testing.

During testing, you can move time forward by calling `Advance(TimeSpan)` or `SetUtcNow(DateTimeOffset)` on `ManualTimeProvider`. This allows you to write tests that run fast and predictably, even if the system under test pauses execution for multiple minutes using e.g. `TimeProvider.Delay(TimeSpan)`, the replacement for `Task.Delay(TimeSpan)`.

## Difference between `ManualTimeProvider` and `FakeTimeProvider`

The .NET team has published a similar test-specific time provider, the [`Microsoft.Extensions.Time.Testing.FakeTimeProvider`](https://www.nuget.org/packages/Microsoft.Extensions.Time.Testing.FakeTimeProvider/).

The public API of both `FakeTimeProvider` and `ManualTimeProvider` are identical, but there are some differences in when time is set before timer callbacks. Let's illustrate this with an example:

For example, if we create an `ITimer` with a *due time* and *period* set to **1 second**, the `DateTimeOffset` returned from `GetUtcNow()` during the timer callback may be different depending on the amount passed to `Advance()` (or `SetUtcNow()`).

If we call `Advance(TimeSpan.FromSeconds(1))` three times, effectively moving time forward by three seconds, the timer callback will be invoked once at times `00:01`, `00:02`, and `00:03`, as illustrated in the drawing below. Both `FakeTimeProvider` and `ManualTimeProvider` behaves like this:

![Advancing time by three seconds in one-second increments.](docs/advance-1-second.svg)

However, if we instead call `Advance(TimeSpan.FromSeconds(3))` once, the two implementations behave differently. `ManualTimeProvider` will invoke the timer callback at the same time as if we had called `Advance(TimeSpan.FromSeconds(1))` three times, as illustrated in the drawing below:

![Advancing time by three seconds in one step using ManualTimeProvider.](docs/ManualTimeProvider-advance-3-seconds.svg)

However, `FakeTimeProvider` will invoke the timer callback at time `00:03` three times, as illustrated in the drawings below:

![Advancing time by three seconds in one step using FakeTimeProvider.](docs/FakeTimeProvider-advance-3-seconds.svg)

Technically, both implementations are correct since the `ITimer` abstractions only promise to invoke the callback timer *on or after the due time/period has elapsed*, never before.

However, I strongly prefer the `ManualTimeProvider` approach since it behaves consistently independent of how time is moved forward. It seems much more in the spirit of how a deterministic time provider should behave and avoids users being surprised when writing tests. I imagine users may get stuck for a while trying to debug why the time reported by `GetUtcNow()` is not set as expected due to the subtle difference in behavior of `FakeTimeProvider`.

## Known limitations:

- If running on .NET versions earlier than .NET 8.0, there is a constraint when invoking `CancellationTokenSource.CancelAfter(TimeSpan)` on the `CancellationTokenSource` object returned by `CreateCancellationTokenSource(TimeSpan delay)`. This action will not terminate the initial timer indicated by the `delay` argument initially passed the `CreateCancellationTokenSource` method. However, this restriction does not apply to .NET 8.0 and later versions.
- To enable controlling `PeriodicTimer` via `TimeProvider` in versions of .NET earlier than .NET 8.0, the `TimeProvider.CreatePeriodicTimer` returns a `PeriodicTimerWrapper` object instead of a `PeriodicTimer` object. The `PeriodicTimerWrapper` type is just a lightweight wrapper around the original `System.Threading.PeriodicTimer` and will behave identically to it. 

## Installation

Get the latest release from https://www.nuget.org/packages/TimeProviderExtensions

## Set up in production

To use in production, pass in `TimeProvider.System` to the types that depend on `TimeProvider`. 
This can be done directly or via an IoC Container, e.g., .NETs built-in `IServiceCollection` like so:

```c#
services.AddSingleton(TimeProvider.System);
```

If you do not want to register the `TimeProvider` with your IoC container, you can instead create
an additional constructor in the types that use it, which allows you to pass in a `TimeProvider`,
and in the existing constructor(s) you have, just new up `TimeProvider.System` directly. For example:

```c#
public class MyService
{
  private readonly TimeProvider timeProvider;
  
  public MyService() : this(TimeProvider.System)
  {
  }
  
  public MyService(TimeProvider timeProvider)
  {
    this.timeProvider = timeProvider;
  }
}
```

This allows you to explicitly pass in a `ManualTimeProvider` during testing.

## Example - control time during tests

If a system under test (SUT) uses things like `Task.Delay`, `DateTimeOffset.UtcNow`, `Task.WaitAsync`, or `PeriodicTimer`, 
it becomes hard to create tests that run fast and predictably.

The idea is to replace the use of e.g. `Task.Delay` with an abstraction, the `TimeProvider`, that in production
is represented by the `TimeProvider.System`, which just uses the real `Task.Delay`. During testing it is now possible to
pass in `ManualTimeProvider`, which allows the test to control the progress of time, making it possible to skip ahead,
e.g. 10 minutes, and also pause time, leading to fast and predictable tests.

As an example, let us test the "Stuff Service" below that performs specific tasks every 10 seconds with an additional 
1-second delay. We have two versions, one that uses the standard types in .NET, and one that uses the `TimeProvider`.

```c#
// Version of stuff service that uses the built-in DateTimeOffset, PeriodicTimer, and Task.Delay
public class StuffService
{
  private static readonly TimeSpan doStuffDelay = TimeSpan.FromSeconds(10);
  private readonly List<DateTimeOffset> container;

  public StuffService(List<DateTimeOffset> container)
  {
    this.container = container;
  }
  
  public async Task DoStuff(CancellationToken cancelllationToken)
  {
    using var periodicTimer = new PeriodicTimer(doStuffDelay);
    
    while (await periodicTimer.WaitForNextTickAsync(cancellationToken))
    {      
      await Task.Delay(TimeSpan.FromSeconds(1));
      container.Add(DateTimeOffset.UtcNow);
    }
  }
}

// Version of stuff service that uses the built-in TimeProvider
public class StuffServiceUsingTimeProvider
{
  private static readonly TimeSpan doStuffDelay = TimeSpan.FromSeconds(10);
  private readonly TimeProvider timeProvider;
  private readonly List<DateTimeOffset> container;

  public StuffServiceUsingTimeProvider(TimeProvider timeProvider, List<DateTimeOffset> container)
  {
    this.timeProvider = timeProvider;
    this.container = container;
  }
  
  public async Task DoStuff(CancellationToken cancelllationToken)
  {
    using var periodicTimer = timeProvider.CreatePeriodicTimer(doStuffDelay);
    
    while (await periodicTimer.WaitForNextTickAsync(cancellationToken))
    {      
      await timeProvider.Delay(TimeSpan.FromSeconds(1));
      container.Add(timeProvider.GetUtcNow());
    }
  }
}
```

The test, using xUnit and FluentAssertions, could look like this:

```c#
[Fact]
public void DoStuff_does_stuff_every_11_seconds()
{
  // Arrange
  var timeProvider = new ManualTimeProvider();
  var container = new List<DateTimeOffset>();  
  var sut = new StuffServiceUsingTimeProvider(timeProvider, container);
  
  // Act
  _ = sut.DoStuff(CancellationToken.None);
  timeProvider.Advance(TimeSpan.FromSeconds(11));
  
  // Assert
  container
    .Should()
    .ContainSingle()
    .Which
    .Should()
    .Be(timeProvider.GetUtcNow());
}
```

This test will run in nanoseconds and is deterministic.

Compare that to the similar test below for `StuffService` that needs to wait for 11 seconds before it can safely assert that the expectation has been met.

```c#
[Fact]
public async Task DoStuff_does_stuff_every_11_seconds()
{
  // Arrange
  var container = new List<DateTimeOffset>();  
  var sut = new StuffService(container);
  
  // Act
  _ = sut.DoStuff(CancellationToken.None);
  await Task.Delay(TimeSpan.FromSeconds(11));
  
  // Assert
  container
    .Should()
    .ContainSingle()
    .Which
    .Should()
    .BeCloseTo(DateTimeOffset.UtcNow, precision: TimeSpan.FromMilliseconds(50));
}
```
