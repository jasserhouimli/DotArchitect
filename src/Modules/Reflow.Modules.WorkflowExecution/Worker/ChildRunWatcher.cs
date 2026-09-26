using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Handlers;
using Reflow.Modules.WorkflowExecution.Persistence;
using Reflow.Modules.WorkflowExecution.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Reflow.Modules.WorkflowExecution.Worker;

/// <summary>
/// Re-queues suspended (Waiting) workflow.call tasks once their child run
/// reaches a terminal state, times out, or the parent run stops. Cheap status
/// polling only; the actual outcome is decided when the worker re-executes
/// the task, so this service never interprets results.
/// </summary>
public class ChildRunWatcher(
    IServiceProvider services,
    WorkerWakeup wakeup,
    ILogger<ChildRunWatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await CheckWaitingAsync(stoppingToken))
                    wakeup.Pulse();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Child watcher iteration failed");
            }

            try
            {
                await wakeup.WaitAsync(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<bool> CheckWaitingAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var execDb = scope.ServiceProvider.GetRequiredService<WorkflowExecutionDbContext>();

        var waiting = await execDb.TaskRuns
            .Where(t => t.Status == TaskRunStatus.Waiting && t.WaitingOnRunId != null)
            .Select(t => new
            {
                t.Id,
                t.WorkflowRunId,
                ChildId = t.WaitingOnRunId!.Value,
                t.StartedAt,
                t.CreatedAt,
                t.ConfigJson
            })
            .Take(50)
            .ToListAsync(ct);

        if (waiting.Count == 0)
            return false;

        var runIds = waiting.Select(w => w.WorkflowRunId).Distinct().ToList();
        var parentStatuses = await execDb.WorkflowRuns
            .Where(r => runIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Status })
            .ToListAsync(ct);
        var parentById = parentStatuses.ToDictionary(r => r.Id, r => r.Status);

        var progress = false;
        foreach (var w in waiting)
        {
            if (ct.IsCancellationRequested) break;

            // Parent run settled (cancelled/failed): let the worker finalize the task.
            if (parentById.TryGetValue(w.WorkflowRunId, out var parentStatus)
                && parentStatus != WorkflowRunStatus.Running)
            {
                progress |= await RequeueAsync(execDb, w.Id, ct);
                continue;
            }

            var child = await execDb.WorkflowRuns
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == w.ChildId, ct);
            var timeout = WorkflowCallHandler.ParseTimeoutSeconds(w.ConfigJson);
            var elapsed = DateTime.UtcNow - (w.StartedAt ?? w.CreatedAt);

            if (child is null
                || child.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled
                || elapsed.TotalSeconds >= timeout)
                progress |= await RequeueAsync(execDb, w.Id, ct);
        }

        return progress;
    }

    private static async Task<bool> RequeueAsync(WorkflowExecutionDbContext execDb, Guid taskId, CancellationToken ct)
    {
        var task = await execDb.TaskRuns.FindAsync([taskId], ct);
        if (task is null || task.Status != TaskRunStatus.Waiting)
            return false;
        task.Status = TaskRunStatus.Ready;
        await execDb.SaveChangesAsync(ct);
        return true;
    }
}
