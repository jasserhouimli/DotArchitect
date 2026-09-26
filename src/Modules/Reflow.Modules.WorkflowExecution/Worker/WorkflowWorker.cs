using System.Text.Json;
using Reflow.Infrastructure.Data;
using Reflow.Infrastructure.Events;
using Reflow.Infrastructure.Expressions;
using Reflow.Infrastructure.Snapshots;
using Reflow.Modules.DataProcessing;
using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Reflow.Modules.WorkflowExecution.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Reflow.Modules.WorkflowExecution.Worker;

public class WorkflowWorker : BackgroundService
{
    public const int MaxAutoAttempts = 3;
    public static readonly TimeSpan TaskTimeout = TimeSpan.FromSeconds(120);

    private readonly IServiceProvider _services;
    private readonly WorkerWakeup _wakeup;
    private readonly ILogger<WorkflowWorker> _logger;

    public WorkflowWorker(IServiceProvider services, WorkerWakeup wakeup, ILogger<WorkflowWorker> logger)
    {
        _services = services;
        _wakeup = wakeup;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkflowWorker started");

        try
        {
            if (await RecoverInterruptedTasks(stoppingToken))
                _wakeup.Pulse();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            // Never let a failed recovery take the whole host down.
            _logger.LogError(ex, "Worker recovery failed; continuing with normal processing");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Pulse on progress so newly-unblocked tasks start immediately
                // instead of waiting out the poll interval. The poll below stays
                // as the correctness net: retry timers, missed pulses, anything
                // the signal path doesn't cover.
                if (await ProcessBatch(stoppingToken))
                    _wakeup.Pulse();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker iteration failed");
            }

            try
            {
                await _wakeup.WaitAsync(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<bool> RecoverInterruptedTasks(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var execDb = scope.ServiceProvider.GetRequiredService<WorkflowExecutionDbContext>();

        var interrupted = await execDb.TaskRuns
            .Where(t => t.Status == TaskRunStatus.Running)
            .ToListAsync(ct);

        foreach (var task in interrupted)
        {
            var attempt = await execDb.TaskAttempts
                .Where(a => a.TaskRunId == task.Id && a.Status == TaskRunStatus.Running)
                .OrderByDescending(a => a.AttemptNumber)
                .FirstOrDefaultAsync(ct);
            if (attempt is not null)
            {
                attempt.Status = TaskRunStatus.Failed;
                attempt.Error = "Worker interrupted";
                attempt.CompletedAt = DateTime.UtcNow;
            }

            task.Status = TaskRunStatus.Ready;
            task.StartedAt = null;
            execDb.ExecutionLogs.Add(new ExecutionLog
            {
                Id = Guid.NewGuid(),
                WorkflowRunId = task.WorkflowRunId,
                TaskRunId = task.Id,
                Message = $"Task {task.NodeId} requeued after worker restart",
                Level = "Warning"
            });
        }

        if (interrupted.Count > 0)
        {
            await execDb.SaveChangesAsync(ct);
            _logger.LogInformation("Requeued {Count} interrupted tasks", interrupted.Count);
        }

        return interrupted.Count > 0;
    }

    private async Task<bool> ProcessBatch(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var execDb = scope.ServiceProvider.GetRequiredService<WorkflowExecutionDbContext>();

        var now = DateTime.UtcNow;
        var candidateIds = await execDb.TaskRuns
            .AsNoTracking()
            .Where(t => t.Status == TaskRunStatus.Ready
                || (t.Status == TaskRunStatus.RetryScheduled && (t.NotBefore == null || t.NotBefore <= now)))
            .OrderBy(t => t.CreatedAt)
            .Select(t => t.Id)
            .Take(5)
            .ToListAsync(ct);

        var progress = false;
        foreach (var id in candidateIds)
        {
            if (ct.IsCancellationRequested) break;

            var claimed = await execDb.Database.ExecuteSqlRawAsync(
                """UPDATE "workflow_execution"."TaskRuns" SET "Status" = 2, "StartedAt" = NOW() WHERE "Id" = {0} AND ("Status" = 1 OR ("Status" = 5 AND ("NotBefore" IS NULL OR "NotBefore" <= NOW())))""",
                new object[] { id }, ct);

            if (claimed == 0) continue;
            progress = true;

            try
            {
                await ExecuteTask(id, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Task {TaskId} execution crashed", id);
            }
        }

        return progress;
    }

    private async Task ExecuteTask(Guid taskId, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var execDb = scope.ServiceProvider.GetRequiredService<WorkflowExecutionDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<TaskHandlerRegistry>();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        async Task Fire(Func<CancellationToken, Task> send)
        {
            try
            {
                await send(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Event publishing must never break execution.
                _logger.LogWarning(ex, "Failed to publish run event");
            }
        }

        var task = await execDb.TaskRuns.FindAsync([taskId], ct);
        if (task is null) return;

        var run = await execDb.WorkflowRuns.FindAsync([task.WorkflowRunId], ct);
        if (run is null)
        {
            task.Status = TaskRunStatus.Failed;
            task.Error = "Parent run not found";
            await execDb.SaveChangesAsync(ct);
            return;
        }

        if (run.Status == WorkflowRunStatus.Cancelled)
        {
            task.Status = TaskRunStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;
            await execDb.SaveChangesAsync(ct);
            var cancelledAttempts = await execDb.TaskAttempts.CountAsync(a => a.TaskRunId == task.Id, ct);
            await Fire(c => bus.PublishAsync(new TaskChangedEvent(run.Id, ToTaskEvent(task, cancelledAttempts)), c));
            return;
        }

        if (run.Status != WorkflowRunStatus.Running)
        {
            task.Status = TaskRunStatus.Skipped;
            task.CompletedAt = DateTime.UtcNow;
            var skipLog = new ExecutionLog
            {
                Id = Guid.NewGuid(), WorkflowRunId = run.Id, TaskRunId = task.Id,
                Message = $"Task {task.NodeId} skipped because run is {run.Status}", Level = "Warning"
            };
            execDb.ExecutionLogs.Add(skipLog);
            await execDb.SaveChangesAsync(ct);
            var skippedAttempts = await execDb.TaskAttempts.CountAsync(a => a.TaskRunId == task.Id, ct);
            await Fire(c => bus.PublishAsync(new LogRecordedEvent(run.Id,
                new LogEventData(skipLog.Id, skipLog.TaskRunId, skipLog.Message, skipLog.Level, skipLog.Timestamp)), c));
            await Fire(c => bus.PublishAsync(new TaskChangedEvent(run.Id, ToTaskEvent(task, skippedAttempts)), c));
            return;
        }

        var attemptNumber = await execDb.TaskAttempts.CountAsync(a => a.TaskRunId == task.Id, ct) + 1;
        var attempt = new TaskAttempt
        {
            Id = Guid.NewGuid(),
            TaskRunId = task.Id,
            AttemptNumber = attemptNumber,
            Status = TaskRunStatus.Running
        };
        execDb.TaskAttempts.Add(attempt);
        await execDb.SaveChangesAsync(ct);

        TaskExecutionResult result;
        ITaskHandler? handler = null;
        var expression = ExpressionResolver.ResolveConfigJson(
            task.ConfigJson,
            new ExpressionContext(run.TriggerKind, run.TriggerName, run.TriggerPayloadJson, run.Id, run.VersionNumber));
        if (!expression.Ok)
        {
            // Deterministic config error: fail fast without consuming retries.
            result = new TaskExecutionResult(false, expression.ValueOrError, null, null);
        }
        else if (!registry.TryGet(task.NodeType, out handler) || handler is null)
        {
            result = new TaskExecutionResult(false, $"No handler for type {task.NodeType}", null, null);
        }
        else
        {
            var inputs = await LoadPredecessorOutputs(run, task.NodeId, execDb, ct);
            var context = new TaskExecutionContext(run.Id, task.Id, task.NodeId, task.NodeType, expression.ValueOrError, inputs, run.WorkflowId, run.TriggerPayloadJson);

            using var taskCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            taskCts.CancelAfter(TaskTimeout);
            try
            {
                result = await handler.ExecuteAsync(context, taskCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                result = new TaskExecutionResult(false, "Task timed out", null, null, handler.SupportsRetry);
            }
            catch (Exception ex)
            {
                result = new TaskExecutionResult(false, Trim(ex.Message), null, null);
            }
        }

        attempt.CompletedAt = DateTime.UtcNow;
        attempt.Error = result.Error;
        attempt.Log = result.Log;

        if (result.WaitForChildRunId is Guid waitingOn)
        {
            // Suspension, not completion: park the task as Waiting. The child
            // watcher re-queues it once the child run finishes; the worker
            // thread is never blocked. The attempt is informational only.
            attempt.Status = TaskRunStatus.Completed;
            task.Status = TaskRunStatus.Waiting;
            task.WaitingOnRunId = waitingOn;
            task.Error = null;
            var waitLog = new ExecutionLog
            {
                Id = Guid.NewGuid(), WorkflowRunId = run.Id, TaskRunId = task.Id,
                Message = result.Log ?? $"Task {task.NodeId} suspended", Level = "Info"
            };
            execDb.ExecutionLogs.Add(waitLog);
            await execDb.SaveChangesAsync(ct);
            await Fire(c => bus.PublishAsync(new LogRecordedEvent(run.Id,
                new LogEventData(waitLog.Id, waitLog.TaskRunId, waitLog.Message, waitLog.Level, waitLog.Timestamp)), c));
            await Fire(c => bus.PublishAsync(new TaskChangedEvent(run.Id, ToTaskEvent(task, attemptNumber)), c));
            await Fire(c => PublishRunProgressAsync(run.Id, c));
            _wakeup.Pulse();
            return;
        }

        if (result.IsSuccess)
        {
            attempt.Status = TaskRunStatus.Completed;
            task.Status = TaskRunStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            task.Error = null;
            task.OutputJson = result.OutputJson;
            task.NotBefore = null;
            var doneLog = new ExecutionLog
            {
                Id = Guid.NewGuid(), WorkflowRunId = run.Id, TaskRunId = task.Id,
                Message = $"Task {task.NodeId} completed: {result.Log}", Level = "Info"
            };
            execDb.ExecutionLogs.Add(doneLog);
            await execDb.SaveChangesAsync(ct);
            await Fire(c => bus.PublishAsync(new LogRecordedEvent(run.Id,
                new LogEventData(doneLog.Id, doneLog.TaskRunId, doneLog.Message, doneLog.Level, doneLog.Timestamp)), c));
            await Fire(c => bus.PublishAsync(new TaskChangedEvent(run.Id, ToTaskEvent(task, attemptNumber)), c));
            await Fire(c => PublishRunProgressAsync(run.Id, c));
            await TryUnblockDownstream(run.Id, execDb, ct);
        }
        else if (handler is not null && handler.SupportsRetry && result.IsRetryable && attemptNumber < MaxAutoAttempts)
        {
            attempt.Status = TaskRunStatus.RetryScheduled;
            task.Status = TaskRunStatus.RetryScheduled;
            task.Error = result.Error;
            task.NotBefore = DateTime.UtcNow.AddSeconds(5 * attemptNumber);
            var retryLog = new ExecutionLog
            {
                Id = Guid.NewGuid(), WorkflowRunId = run.Id, TaskRunId = task.Id,
                Message = $"Task {task.NodeId} failed (attempt {attemptNumber}): {result.Error}. Retry scheduled.", Level = "Warning"
            };
            execDb.ExecutionLogs.Add(retryLog);
            await execDb.SaveChangesAsync(ct);
            await Fire(c => bus.PublishAsync(new LogRecordedEvent(run.Id,
                new LogEventData(retryLog.Id, retryLog.TaskRunId, retryLog.Message, retryLog.Level, retryLog.Timestamp)), c));
            await Fire(c => bus.PublishAsync(new TaskChangedEvent(run.Id, ToTaskEvent(task, attemptNumber)), c));
            await Fire(c => PublishRunProgressAsync(run.Id, c));
        }
        else
        {
            attempt.Status = TaskRunStatus.Failed;
            task.Status = TaskRunStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            task.Error = result.Error;
            task.NotBefore = null;
            run.Status = WorkflowRunStatus.Failed;
            run.CompletedAt = DateTime.UtcNow;
            run.Error = $"Task {task.NodeId} failed: {result.Error}";
            var failLog = new ExecutionLog
            {
                Id = Guid.NewGuid(), WorkflowRunId = run.Id, TaskRunId = task.Id,
                Message = $"Task {task.NodeId} failed: {result.Error}", Level = "Error"
            };
            execDb.ExecutionLogs.Add(failLog);
            await execDb.SaveChangesAsync(ct);
            await Fire(c => bus.PublishAsync(new LogRecordedEvent(run.Id,
                new LogEventData(failLog.Id, failLog.TaskRunId, failLog.Message, failLog.Level, failLog.Timestamp)), c));
            await Fire(c => bus.PublishAsync(new TaskChangedEvent(run.Id, ToTaskEvent(task, attemptNumber)), c));
            var skipped = await SkipDownstream(run.Id, execDb, ct);
            foreach (var (skippedTaskId, skipLog) in skipped)
            {
                await Fire(c => bus.PublishAsync(new LogRecordedEvent(run.Id,
                    new LogEventData(skipLog.Id, skipLog.TaskRunId, skipLog.Message, skipLog.Level, skipLog.Timestamp)), c));
                var skippedAttempts = await execDb.TaskAttempts.CountAsync(a => a.TaskRunId == skippedTaskId, ct);
                var skippedTask = await execDb.TaskRuns.FindAsync([skippedTaskId], ct);
                if (skippedTask is not null)
                    await Fire(c => bus.PublishAsync(new TaskChangedEvent(run.Id, ToTaskEvent(skippedTask, skippedAttempts)), c));
            }
            await Fire(c => PublishRunProgressAsync(run.Id, c));
            await Fire(c => PublishRunFinishedAsync(run.Id, c));
        }

        var completionLog = await TryFinalizeRun(run.Id, execDb, ct);
        if (completionLog is not null)
        {
            await Fire(c => bus.PublishAsync(new LogRecordedEvent(run.Id,
                new LogEventData(completionLog.Id, completionLog.TaskRunId, completionLog.Message, completionLog.Level, completionLog.Timestamp)), c));
            await Fire(c => PublishRunProgressAsync(run.Id, c));
            await Fire(c => PublishRunFinishedAsync(run.Id, c));
        }
    }

    private static TaskEventData ToTaskEvent(TaskRun task, int attemptCount)
    {
        var output = task.OutputJson is not null ? Dataset.TryFromJson(task.OutputJson) : null;
        return new TaskEventData(task.Id, task.WorkflowRunId, task.NodeId, task.NodeType,
            (int)task.Status, task.Error, task.CreatedAt, task.StartedAt, task.CompletedAt,
            attemptCount, output?.Summary(), output?.Rows.Count);
    }

    private async Task PublishRunProgressAsync(Guid runId, CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowExecutionDbContext>();
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            var run = await db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
            if (run is null) return;
            var statuses = await db.TaskRuns.AsNoTracking()
                .Where(t => t.WorkflowRunId == runId)
                .Select(t => t.Status)
                .ToListAsync(ct);
            await bus.PublishAsync(new RunProgressEvent(new RunEventData(run.Id, run.WorkflowId,
                run.VersionNumber, (int)run.Status, run.CreatedAt, run.StartedAt, run.CompletedAt, run.Error,
                statuses.Count,
                statuses.Count(s => s == TaskRunStatus.Completed),
                statuses.Count(s => s == TaskRunStatus.Failed))), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish run progress for run {RunId}", runId);
        }
    }

    private async Task PublishRunFinishedAsync(Guid runId, CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<WorkflowExecutionDbContext>();
            var snapshots = sp.GetRequiredService<IWorkflowSnapshotProvider>();
            var bus = sp.GetRequiredService<IEventBus>();
            var run = await db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
            if (run is null) return;
            var tasks = await db.TaskRuns.AsNoTracking().Where(t => t.WorkflowRunId == runId).ToListAsync(ct);
            var rejected = 0;
            foreach (var t in tasks)
            {
                if (t.OutputJson is not null && Dataset.TryFromJson(t.OutputJson) is { } dataset)
                    rejected += dataset.Quality.RejectedCount;
            }
            var name = "Workflow";
            var snapshot = await snapshots.GetPublishedSnapshotAsync(run.WorkflowId, run.CreatedBy, ct);
            if (snapshot.IsSuccess && snapshot.Value is not null)
                name = snapshot.Value.Name;
            await bus.PublishAsync(new RunFinishedEvent(run.Id, run.WorkflowId, run.CreatedBy, name,
                run.VersionNumber, run.Status == WorkflowRunStatus.Completed,
                tasks.Count,
                tasks.Count(t => t.Status == TaskRunStatus.Completed),
                tasks.Count(t => t.Status == TaskRunStatus.Failed),
                rejected, run.Error, run.CreatedAt, run.StartedAt, run.CompletedAt), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish run finished event for run {RunId}", runId);
        }
    }

    private static async Task<List<Dataset>> LoadPredecessorOutputs(WorkflowRun run, string nodeId, WorkflowExecutionDbContext execDb, CancellationToken ct)
    {
        var inputs = new List<Dataset>();
        foreach (var predId in GetPredecessors(run.EdgesJson, nodeId))
        {
            var pred = await execDb.TaskRuns
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.WorkflowRunId == run.Id && t.NodeId == predId && t.Status == TaskRunStatus.Completed, ct);
            if (pred?.OutputJson is not null && Dataset.TryFromJson(pred.OutputJson) is { } dataset)
                inputs.Add(dataset);
        }
        return inputs;
    }

    private static List<string> GetPredecessors(string? edgesJson, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(edgesJson)) return new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(edgesJson);
            var result = new List<string>();
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.TryGetProperty("target", out var t) && t.GetString() == nodeId
                    && e.TryGetProperty("source", out var s) && s.GetString() is { } source)
                    result.Add(source);
            }
            return result;
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static async Task TryUnblockDownstream(Guid runId, WorkflowExecutionDbContext execDb, CancellationToken ct)
    {
        var run = await execDb.WorkflowRuns.FindAsync([runId], ct);
        if (run is null) return;

        var taskRuns = await execDb.TaskRuns.Where(t => t.WorkflowRunId == runId).ToListAsync(ct);
        var completedIds = taskRuns.Where(t => t.Status == TaskRunStatus.Completed).Select(t => t.NodeId).ToHashSet();

        foreach (var task in taskRuns.Where(t => t.Status == TaskRunStatus.Pending))
        {
            var predecessors = GetPredecessors(run.EdgesJson, task.NodeId);
            if (predecessors.Count == 0 || predecessors.All(p => completedIds.Contains(p)))
                task.Status = TaskRunStatus.Ready;
        }

        await execDb.SaveChangesAsync(ct);
    }

    private static async Task<List<(Guid TaskId, ExecutionLog Log)>> SkipDownstream(Guid runId, WorkflowExecutionDbContext execDb, CancellationToken ct)
    {
        var tasks = await execDb.TaskRuns
            .Where(t => t.WorkflowRunId == runId
                && (t.Status == TaskRunStatus.Pending || t.Status == TaskRunStatus.Ready || t.Status == TaskRunStatus.RetryScheduled))
            .ToListAsync(ct);

        var skipped = new List<(Guid, ExecutionLog)>();
        foreach (var task in tasks)
        {
            task.Status = TaskRunStatus.Skipped;
            task.CompletedAt = DateTime.UtcNow;
            var log = new ExecutionLog
            {
                Id = Guid.NewGuid(), WorkflowRunId = runId, TaskRunId = task.Id,
                Message = $"Task {task.NodeId} skipped because an upstream task failed", Level = "Warning"
            };
            execDb.ExecutionLogs.Add(log);
            skipped.Add((task.Id, log));
        }

        await execDb.SaveChangesAsync(ct);
        return skipped;
    }

    private static async Task<ExecutionLog?> TryFinalizeRun(Guid runId, WorkflowExecutionDbContext execDb, CancellationToken ct)
    {
        var run = await execDb.WorkflowRuns.FindAsync([runId], ct);
        if (run is null || run.Status != WorkflowRunStatus.Running) return null;

        var tasks = await execDb.TaskRuns.Where(t => t.WorkflowRunId == runId).ToListAsync(ct);
        if (tasks.Count == 0 || !tasks.All(t => IsTerminal(t.Status))) return null;

        run.Status = WorkflowRunStatus.Completed;
        run.CompletedAt = DateTime.UtcNow;
        var log = new ExecutionLog
        {
            Id = Guid.NewGuid(), WorkflowRunId = run.Id,
            Message = $"Run {run.Id} completed ({tasks.Count(t => t.Status == TaskRunStatus.Completed)}/{tasks.Count} tasks succeeded)", Level = "Info"
        };
        execDb.ExecutionLogs.Add(log);
        await execDb.SaveChangesAsync(ct);
        return log;
    }

    private static bool IsTerminal(TaskRunStatus status) => status is
        TaskRunStatus.Completed or TaskRunStatus.Failed or TaskRunStatus.Cancelled or TaskRunStatus.Skipped;

    private static string Trim(string message)
    {
        var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Length > 300 ? oneLine[..300] : oneLine;
    }
}
