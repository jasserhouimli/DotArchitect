using Reflow.Infrastructure.Results;
using Reflow.Modules.WorkflowDesign.Persistence;
using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.StartWorkflowRun;

public class StartWorkflowRunHandler(WorkflowDesignDbContext designDb, WorkflowExecutionDbContext execDb)
{
    public async Task<Result<Guid>> Handle(Guid workflowId, Guid userId, CancellationToken ct)
    {
        var workflow = await designDb.Workflows.Include(w => w.Nodes).Include(w => w.Edges)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerId == userId, ct);
        if (workflow is null) return Result<Guid>.Failure("Workflow not found", 404);
        if (workflow.Status != Reflow.Modules.WorkflowDesign.Domain.WorkflowStatus.Published) return Result<Guid>.Failure("Workflow must be published before running", 400);

        var version = await designDb.WorkflowVersions
            .FirstOrDefaultAsync(v => v.WorkflowId == workflowId && v.VersionNumber == workflow.CurrentVersion, ct);
        if (version is null) return Result<Guid>.Failure("Published version not found", 404);

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(), WorkflowId = workflowId, WorkflowVersionId = version.Id,
            VersionNumber = workflow.CurrentVersion, Status = WorkflowRunStatus.Queued,
            CreatedBy = userId, CreatedAt = DateTime.UtcNow
        };
        execDb.WorkflowRuns.Add(run);

        // Create TaskRuns for each node
        foreach (var node in workflow.Nodes)
        {
            execDb.TaskRuns.Add(new TaskRun
            {
                Id = Guid.NewGuid(), WorkflowRunId = run.Id,
                NodeId = node.NodeId, NodeType = node.NodeType,
                Status = TaskRunStatus.Pending
            });
        }

        execDb.ExecutionLogs.Add(new ExecutionLog
        {
            Id = Guid.NewGuid(), WorkflowRunId = run.Id,
            Message = $"Run {run.Id} queued for workflow {workflow.Name} v{workflow.CurrentVersion}",
            Level = "Info"
        });

        await execDb.SaveChangesAsync(ct);

        // Mark root tasks as Ready (no incoming edges)
        var edgeTargets = workflow.Edges.Select(e => e.TargetNodeId).ToHashSet();
        var rootTaskRuns = await execDb.TaskRuns.Where(t => t.WorkflowRunId == run.Id && !edgeTargets.Contains(t.NodeId)).ToListAsync(ct);
        foreach (var t in rootTaskRuns) t.Status = TaskRunStatus.Ready;
        run.Status = WorkflowRunStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        await execDb.SaveChangesAsync(ct);

        return Result<Guid>.Success(run.Id, 201);
    }
}
