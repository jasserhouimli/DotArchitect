using System.Diagnostics;
using Reflow.Modules.WorkflowExecution.Services;
using Xunit;

namespace Reflow.UnitTests;

public class WorkerWakeupTests
{
    [Fact]
    public async Task Pulse_BeforeWait_ReturnsImmediately()
    {
        var wakeup = new WorkerWakeup();
        wakeup.Pulse();

        var sw = Stopwatch.StartNew();
        await wakeup.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"Took {sw.Elapsed}");
    }

    [Fact]
    public async Task Pulse_WakesActiveWaiter()
    {
        var wakeup = new WorkerWakeup();
        var wait = wakeup.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        await Task.Delay(50);

        var sw = Stopwatch.StartNew();
        wakeup.Pulse();
        await wait;
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"Took {sw.Elapsed}");
    }

    [Fact]
    public async Task NoPulse_WaitsFullInterval()
    {
        var wakeup = new WorkerWakeup();

        var sw = Stopwatch.StartNew();
        await wakeup.WaitAsync(TimeSpan.FromMilliseconds(300), CancellationToken.None);
        sw.Stop();

        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(200), $"Returned after {sw.Elapsed}");
    }

    [Fact]
    public async Task Pulses_CoalesceIntoOneWakeUp()
    {
        var wakeup = new WorkerWakeup();
        wakeup.Pulse();
        wakeup.Pulse();
        wakeup.Pulse();

        await wakeup.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        // The extra pulses were consumed: the next wait must block again.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => wakeup.WaitAsync(TimeSpan.FromSeconds(10), cts.Token));
        sw.Stop();
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(200), $"Returned after {sw.Elapsed}");
    }

    [Fact]
    public async Task Cancellation_ThrowsLikeTaskDelay()
    {
        var wakeup = new WorkerWakeup();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => wakeup.WaitAsync(TimeSpan.FromSeconds(30), cts.Token));
    }

    [Fact]
    public void ConcurrentPulses_DoNotThrow()
    {
        var wakeup = new WorkerWakeup();
        Parallel.For(0, 1000, _ => wakeup.Pulse());
    }
}
