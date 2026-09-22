using Reflow.Modules.WorkflowExecution.Handlers;
using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Reflow.Modules.WorkflowDesign.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Reflow.Modules.WorkflowExecution.Worker;

public class WorkflowWorker : BackgroundService
{
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessReadyTasks(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker iteration failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task ProcessReadyTasks(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var execDb = scope.ServiceProvider.GetRequiredService<WorkflowExecutionDbContext>();
        var designDb = scope.ServiceProvider.GetRequiredService<WorkflowDesignDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<TaskHandlerRegistry>();

        var readyTasks = await execDb.TaskRuns
            .Where(t => t.Status == TaskRunStatus.Ready)
            .Take(5)
            .ToListAsync(ct);

        foreach (var task in readyTasks)
        {
            task.Status = TaskRunStatus.Running;
            task.StartedAt = DateTime.UtcNow;
            var attempt = new TaskAttempt
            {
                Id = Guid.NewGuid(),
                TaskRunId = task.Id,
                AttemptNumber = await execDb.TaskAttempts.CountAsync(a => a.TaskRunId == task.Id, ct) + 1,
                Status = TaskRunStatus.Running
            };
            execDb.TaskAttempts.Add(attempt);
            await execDb.SaveChangesAsync(ct);

            TaskExecutionResult result;
            try
            {
                if (!registry.TryGet(task.NodeType, out var handler) || handler is null)
                {
                    result = new TaskExecutionResult(false, $"No handler for type {task.NodeType}", null, null);
                }
                else
                {
                    var ctx = new TaskExecutionContext(task.WorkflowRunId, task.Id, task.NodeId, task.NodeType, null);
                    result = await handler.ExecuteAsync(ctx, ct);
                }
            }
            catch (Exception ex)
            {
                result = new TaskExecutionResult(false, ex.Message, null, null);
            }

            attempt.CompletedAt = DateTime.UtcNow;
            attempt.Status = result.IsSuccess ? TaskRunStatus.Completed : TaskRunStatus.Failed;
            attempt.Error = result.Error;
            attempt.Log = result.Log;

            task.Status = result.IsSuccess ? TaskRunStatus.Completed : TaskRunStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            task.Error = result.Error;

            execDb.ExecutionLogs.Add(new ExecutionLog
            {
                Id = Guid.NewGuid(),
                WorkflowRunId = task.WorkflowRunId,
                TaskRunId = task.Id,
                Message = result.IsSuccess ? $"Task {task.NodeId} completed" : $"Task {task.NodeId} failed: {result.Error}",
                Level = result.IsSuccess ? "Info" : "Error"
            });

            await execDb.SaveChangesAsync(ct);

            if (result.IsSuccess)
            {
                await TryUnblockDownstream(task.WorkflowRunId, execDb, designDb, ct);
            }
            else
            {
                // Mark run as failed if any task fails (fail-fast for now)
                var run = await execDb.WorkflowRuns.FindAsync([task.WorkflowRunId], ct);
                if (run is not null)
                {
                    run.Status = WorkflowRunStatus.Failed;
                    run.CompletedAt = DateTime.UtcNow;
                    run.Error = $"Task {task.NodeId} failed: {result.Error}";
                    await execDb.SaveChangesAsync(ct);
                }
            }

            // Check if all tasks completed -> mark run completed
            var allTasks = await execDb.TaskRuns.Where(t => t.WorkflowRunId == task.WorkflowRunId).ToListAsync(ct);
            if (allTasks.All(t => t.Status == TaskRunStatus.Completed))
            {
                var run = await execDb.WorkflowRuns.FindAsync([task.WorkflowRunId], ct);
                if (run is not null && run.Status == WorkflowRunStatus.Running)
                {
                    run.Status = WorkflowRunStatus.Completed;
                    run.CompletedAt = DateTime.UtcNow;
                    execDb.ExecutionLogs.Add(new ExecutionLog
                    {
                        Id = Guid.NewGuid(), WorkflowRunId = run.Id,
                        Message = $"Run {run.Id} completed", Level = "Info"
                    });
                    await execDb.SaveChangesAsync(ct);
                }
            }
        }
    }

    private static async Task TryUnblockDownstream(Guid runId, WorkflowExecutionDbContext execDb, WorkflowDesignDbContext designDb, CancellationToken ct)
    {
        var run = await execDb.WorkflowRuns.FindAsync([runId], ct);
        if (run is null) return;

        var version = await designDb.WorkflowVersions.FirstOrDefaultAsync(v => v.Id == run.WorkflowVersionId, ct);
        if (version is null) return;

        // Parse version definition to get edges
        // For now use WorkflowEdges from designDb via workflowId
        var edges = await designDb.WorkflowEdges.Where(e => e.WorkflowId == run.WorkflowId).ToListAsync(ct);
        var taskRuns = await execDb.TaskRuns.Where(t => t.WorkflowRunId == runId).ToListAsync(ct);
        var completedIds = taskRuns.Where(t => t.Status == TaskRunStatus.Completed).Select(t => t.NodeId).ToHashSet();

        foreach (var task in taskRuns.Where(t => t.Status == TaskRunStatus.Pending))
        {
            var predecessors = edges.Where(e => e.TargetNodeId == task.NodeId).Select(e => e.SourceNodeId).ToList();
            if (predecessors.Count == 0 || predecessors.All(p => completedIds.Contains(p)))
            {
                task.Status = TaskRunStatus.Ready;
            }
        }

        await execDb.SaveChangesAsync(ct);
    }
}
