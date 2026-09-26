namespace Reflow.Modules.WorkflowExecution.Services;

/// <summary>
/// Best-effort wake-up signal for the background <c>WorkflowWorker</c>.
/// Pulses are a pure latency optimization, never a correctness mechanism:
/// the worker's timed poll remains the safety net (retry timers becoming due,
/// missed signals, newly started runs), so a lost or coalesced pulse can
/// only cost one poll interval, never a stuck task.
/// </summary>
public sealed class WorkerWakeup
{
    private TaskCompletionSource _gate = NewGate();

    /// <summary>
    /// Wakes a currently-waiting worker immediately. Multiple pulses coalesce
    /// into one wake-up; a pulse with no waiter is remembered until the next
    /// <see cref="WaitAsync"/> call consumes it. Thread-safe.
    /// </summary>
    public void Pulse()
    {
        Volatile.Read(ref _gate).TrySetResult();
    }

    /// <summary>
    /// Waits for a pulse or for <paramref name="pollInterval"/> to elapse,
    /// whichever comes first. Throws <see cref="OperationCanceledException"/>
    /// on cancellation, matching the old <c>Task.Delay</c> behavior.
    /// </summary>
    public async Task WaitAsync(TimeSpan pollInterval, CancellationToken ct)
    {
        var gate = Volatile.Read(ref _gate);
        var winner = await Task.WhenAny(gate.Task, Task.Delay(pollInterval, ct)).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (ReferenceEquals(winner, gate.Task))
            Interlocked.CompareExchange(ref _gate, NewGate(), gate);
        await winner.ConfigureAwait(false);
    }

    private static TaskCompletionSource NewGate() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
