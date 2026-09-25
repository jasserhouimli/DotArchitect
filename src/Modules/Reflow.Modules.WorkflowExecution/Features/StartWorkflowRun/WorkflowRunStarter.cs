using System.Text.Json;
using Reflow.Infrastructure.Results;
using Reflow.Infrastructure.Runs;
using Reflow.Infrastructure.Snapshots;
using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.StartWorkflowRun;

public class WorkflowRunStarter(IWorkflowSnapshotProvider snapshots, WorkflowExecutionDbContext execDb)
    : IWorkflowRunStarter
{
    public const int MaxTriggerPayloadChars = 512 * 1024;

    public async Task<Result<Guid>> StartRunAsync(Guid workflowId, Guid ownerId, RunTrigger? trigger, CancellationToken ct)
    {
        var snapshot = await snapshots.GetPublishedSnapshotAsync(workflowId, ownerId, ct);
        if (!snapshot.IsSuccess || snapshot.Value is null)
            return Result<Guid>.Failure(snapshot.Error ?? "Workflow not found", snapshot.StatusCode);

        var definition = snapshot.Value;
        var kind = trigger?.Kind ?? "manual";
        if (kind != "manual" && kind != "schedule" && kind != "webhook")
            return Result<Guid>.Failure($"Unknown trigger kind: {kind}", 400);

        string? payload = trigger?.PayloadJson;
        if (payload is not null && payload.Length > MaxTriggerPayloadChars)
            return Result<Guid>.Failure($"Trigger payload too large (max {MaxTriggerPayloadChars} chars)", 400);
        if (payload is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
            }
            catch (JsonException)
            {
                return Result<Guid>.Failure("Trigger payload must be valid JSON", 400);
            }
        }

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            WorkflowVersionId = definition.VersionId,
            VersionNumber = definition.VersionNumber,
            Status = WorkflowRunStatus.Queued,
            CreatedBy = ownerId,
            CreatedAt = DateTime.UtcNow,
            EdgesJson = JsonSerializer.Serialize(definition.Edges.Select(e => new { source = e.SourceNodeId, target = e.TargetNodeId })),
            TriggerKind = kind,
            TriggerName = trigger?.Name,
            TriggerPayloadJson = payload
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
            Message = $"Run {run.Id} queued for workflow {definition.Name} v{definition.VersionNumber} (trigger: {kind})",
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
