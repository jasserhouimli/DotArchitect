using System.Text.Json;
using Reflow.Modules.WorkflowExecution.Data;
using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Handlers;
using Reflow.Modules.WorkflowExecution.Persistence;
using Reflow.Modules.WorkflowDesign.Persistence;
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
    private readonly ILogger<WorkflowWorker> _logger;

    public WorkflowWorker(IServiceProvider services, ILogger<WorkflowWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkflowWorker started");
        await RecoverInterruptedTasks(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatch(stoppingToken);
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
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RecoverInterruptedTasks(CancellationToken ct)
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
    }

    private async Task ProcessBatch(CancellationToken ct)
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

        foreach (var id in candidateIds)
        {
            if (ct.IsCancellationRequested) break;

            var claimed = await execDb.Database.ExecuteSqlRawAsync(
                """UPDATE "workflow_execution"."TaskRuns" SET "Status" = 2, "StartedAt" = NOW() WHERE "Id" = {0} AND ("Status" = 1 OR ("Status" = 5 AND ("NotBefore" IS NULL OR "NotBefore" <= NOW())))""",
                new object[] { id }, ct);

            if (claimed == 0) continue;

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
    }

    private async Task ExecuteTask(Guid taskId, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var execDb = scope.ServiceProvider.GetRequiredService<WorkflowExecutionDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<TaskHandlerRegistry>();

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
            return;
        }

        if (run.Status != WorkflowRunStatus.Running)
        {
            task.Status = TaskRunStatus.Skipped;
            task.CompletedAt = DateTime.UtcNow;
            execDb.ExecutionLogs.Add(new ExecutionLog
            {
                Id = Guid.NewGuid(), WorkflowRunId = run.Id, TaskRunId = task.Id,
                Message = $"Task {task.NodeId} skipped because run is {run.Status}", Level = "Warning"
            });
            await execDb.SaveChangesAsync(ct);
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
        if (!registry.TryGet(task.NodeType, out var handler) || handler is null)
        {
            result = new TaskExecutionResult(false, $"No handler for type {task.NodeType}", null, null);
        }
        else
        {
            var inputs = await LoadPredecessorOutputs(run, task.NodeId, execDb, ct);
            var context = new TaskExecutionContext(run.Id, task.Id, task.NodeId, task.NodeType, task.ConfigJson, inputs);

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

        if (result.IsSuccess)
        {
            attempt.Status = TaskRunStatus.Completed;
            task.Status = TaskRunStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            task.Error = null;
            task.OutputJson = result.OutputJson;
            task.NotBefore = null;
            execDb.ExecutionLogs.Add(new ExecutionLog
            {
                Id = Guid.NewGuid(), WorkflowRunId = run.Id, TaskRunId = task.Id,
                Message = $"Task {task.NodeId} completed: {result.Log}", Level = "Info"
            });
            await execDb.SaveChangesAsync(ct);
            await TryUnblockDownstream(run.Id, execDb, ct);
        }
        else if (handler is not null && handler.SupportsRetry && result.IsRetryable && attemptNumber < MaxAutoAttempts)
        {
            attempt.Status = TaskRunStatus.RetryScheduled;
            task.Status = TaskRunStatus.RetryScheduled;
            task.Error = result.Error;
            task.NotBefore = DateTime.UtcNow.AddSeconds(5 * attemptNumber);
            execDb.ExecutionLogs.Add(new ExecutionLog
            {
                Id = Guid.NewGuid(), WorkflowRunId = run.Id, TaskRunId = task.Id,
                Message = $"Task {task.NodeId} failed (attempt {attemptNumber}): {result.Error}. Retry scheduled.", Level = "Warning"
            });
            await execDb.SaveChangesAsync(ct);
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
            execDb.ExecutionLogs.Add(new ExecutionLog
            {
                Id = Guid.NewGuid(), WorkflowRunId = run.Id, TaskRunId = task.Id,
                Message = $"Task {task.NodeId} failed: {result.Error}", Level = "Error"
            });
            await execDb.SaveChangesAsync(ct);
            await SkipDownstream(run.Id, execDb, ct);
        }

        await TryFinalizeRun(run.Id, execDb, ct);
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

    private static async Task SkipDownstream(Guid runId, WorkflowExecutionDbContext execDb, CancellationToken ct)
    {
        var tasks = await execDb.TaskRuns
            .Where(t => t.WorkflowRunId == runId
                && (t.Status == TaskRunStatus.Pending || t.Status == TaskRunStatus.Ready || t.Status == TaskRunStatus.RetryScheduled))
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            task.Status = TaskRunStatus.Skipped;
            task.CompletedAt = DateTime.UtcNow;
            execDb.ExecutionLogs.Add(new ExecutionLog
            {
                Id = Guid.NewGuid(), WorkflowRunId = runId, TaskRunId = task.Id,
                Message = $"Task {task.NodeId} skipped because an upstream task failed", Level = "Warning"
            });
        }

        await execDb.SaveChangesAsync(ct);
    }

    private static async Task TryFinalizeRun(Guid runId, WorkflowExecutionDbContext execDb, CancellationToken ct)
    {
        var run = await execDb.WorkflowRuns.FindAsync([runId], ct);
        if (run is null || run.Status != WorkflowRunStatus.Running) return;

        var tasks = await execDb.TaskRuns.Where(t => t.WorkflowRunId == runId).ToListAsync(ct);
        if (tasks.Count == 0 || !tasks.All(t => IsTerminal(t.Status))) return;

        run.Status = WorkflowRunStatus.Completed;
        run.CompletedAt = DateTime.UtcNow;
        execDb.ExecutionLogs.Add(new ExecutionLog
        {
            Id = Guid.NewGuid(), WorkflowRunId = run.Id,
            Message = $"Run {run.Id} completed ({tasks.Count(t => t.Status == TaskRunStatus.Completed)}/{tasks.Count} tasks succeeded)", Level = "Info"
        });
        await execDb.SaveChangesAsync(ct);
    }

    private static bool IsTerminal(TaskRunStatus status) => status is
        TaskRunStatus.Completed or TaskRunStatus.Failed or TaskRunStatus.Cancelled or TaskRunStatus.Skipped;

    private static string Trim(string message)
    {
        var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Length > 300 ? oneLine[..300] : oneLine;
    }
}
