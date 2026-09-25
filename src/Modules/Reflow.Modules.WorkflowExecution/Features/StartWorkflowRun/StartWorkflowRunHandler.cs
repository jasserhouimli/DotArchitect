using System.Text.Json;
using Reflow.Infrastructure.Results;
using Reflow.Infrastructure.Snapshots;
using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.StartWorkflowRun;

public class StartWorkflowRunHandler(IWorkflowSnapshotProvider snapshots, WorkflowExecutionDbContext execDb)
{
    public async Task<Result<Guid>> Handle(Guid workflowId, Guid userId, CancellationToken ct)
    {
        var snapshot = await snapshots.GetPublishedSnapshotAsync(workflowId, userId, ct);
        if (!snapshot.IsSuccess || snapshot.Value is null)
            return Result<Guid>.Failure(snapshot.Error ?? "Workflow not found", snapshot.StatusCode);

        var definition = snapshot.Value;
        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            WorkflowVersionId = definition.VersionId,
            VersionNumber = definition.VersionNumber,
            Status = WorkflowRunStatus.Queued,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            EdgesJson = JsonSerializer.Serialize(definition.Edges.Select(e => new { source = e.SourceNodeId, target = e.TargetNodeId }))
        };
        execDb.WorkflowRuns.Add(run);

        foreach (var node in definition.Nodes)
        {
            execDb.TaskRuns.Add(new TaskRun
            {
                Id = Guid.NewGuid(),
                WorkflowRunId = run.Id,
                NodeId = node.NodeId,
                NodeType = node.NodeType,
                ConfigJson = node.ConfigJson,
                Status = TaskRunStatus.Pending
            });
        }

        execDb.ExecutionLogs.Add(new ExecutionLog
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = run.Id,
            Message = $"Run {run.Id} queued for workflow {definition.Name} v{definition.VersionNumber}",
            Level = "Info"
        });

        await execDb.SaveChangesAsync(ct);

        var edgeTargets = definition.Edges.Select(e => e.TargetNodeId).ToHashSet();
        var rootTaskRuns = await execDb.TaskRuns
            .Where(t => t.WorkflowRunId == run.Id && !edgeTargets.Contains(t.NodeId))
            .ToListAsync(ct);
        foreach (var t in rootTaskRuns) t.Status = TaskRunStatus.Ready;
        run.Status = WorkflowRunStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        await execDb.SaveChangesAsync(ct);

        return Result<Guid>.Success(run.Id, 201);
    }
}
