using System.Text.Json;
using Reflow.Infrastructure.Results;
using Reflow.Modules.WorkflowDesign.Persistence;
using Reflow.Modules.WorkflowDesign.Validation;
using Reflow.Modules.WorkflowExecution.Domain;
using Reflow.Modules.WorkflowExecution.Persistence;
using Reflow.Modules.WorkflowExecution.Services;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowExecution.Features.StartWorkflowRun;

public class StartWorkflowRunHandler(WorkflowDesignDbContext designDb, WorkflowExecutionDbContext execDb, RunEventPublisher events)
{
    public async Task<Result<Guid>> Handle(Guid workflowId, Guid userId, CancellationToken ct)
    {
        var workflow = await designDb.Workflows
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerId == userId, ct);
        if (workflow is null) return Result<Guid>.Failure("Workflow not found", 404);
        if (workflow.Status != Reflow.Modules.WorkflowDesign.Domain.WorkflowStatus.Published)
            return Result<Guid>.Failure("Workflow must be published before running", 400);

        var version = await designDb.WorkflowVersions
            .FirstOrDefaultAsync(v => v.WorkflowId == workflowId && v.VersionNumber == workflow.CurrentVersion, ct);
        if (version is null) return Result<Guid>.Failure("Published version not found", 404);

        if (!TryParseSnapshot(version.DefinitionJson, out var nodes, out var edges))
            return Result<Guid>.Failure("Published version is corrupted", 500);

        var validation = WorkflowValidator.Validate(nodes, edges);
        if (!validation.IsValid)
            return Result<Guid>.Failure("Published version is invalid: " + string.Join("; ", validation.Errors), 400);

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            WorkflowVersionId = version.Id,
            VersionNumber = workflow.CurrentVersion,
            Status = WorkflowRunStatus.Queued,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            EdgesJson = JsonSerializer.Serialize(edges.Select(e => new { source = e.SourceNodeId, target = e.TargetNodeId }))
        };
        execDb.WorkflowRuns.Add(run);

        foreach (var node in nodes)
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
            Message = $"Run {run.Id} queued for workflow {workflow.Name} v{workflow.CurrentVersion}",
            Level = "Info"
        });

        await execDb.SaveChangesAsync(ct);

        var edgeTargets = edges.Select(e => e.TargetNodeId).ToHashSet();
        var rootTaskRuns = await execDb.TaskRuns
            .Where(t => t.WorkflowRunId == run.Id && !edgeTargets.Contains(t.NodeId))
            .ToListAsync(ct);
        foreach (var t in rootTaskRuns) t.Status = TaskRunStatus.Ready;
        run.Status = WorkflowRunStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        await execDb.SaveChangesAsync(ct);

        await events.RunUpdated(run.Id, userId, ct);
        return Result<Guid>.Success(run.Id, 201);
    }

    private static bool TryParseSnapshot(string definitionJson, out List<NodeInput> nodes, out List<EdgeInput> edges)
    {
        nodes = new List<NodeInput>();
        edges = new List<EdgeInput>();
        try
        {
            using var doc = JsonDocument.Parse(definitionJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Nodes", out var nodesEl) || nodesEl.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var n in nodesEl.EnumerateArray())
            {
                var nodeId = n.TryGetProperty("NodeId", out var id) ? id.GetString() : null;
                var nodeType = n.TryGetProperty("NodeType", out var type) ? type.GetString() : null;
                if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(nodeType))
                    return false;
                string? config = null;
                if (n.TryGetProperty("ConfigJson", out var cfg) && cfg.ValueKind == JsonValueKind.String)
                    config = cfg.GetString();
                nodes.Add(new NodeInput(nodeId, nodeType, config));
            }

            if (root.TryGetProperty("Edges", out var edgesEl) && edgesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in edgesEl.EnumerateArray())
                {
                    var source = e.TryGetProperty("SourceNodeId", out var s) ? s.GetString() : null;
                    var target = e.TryGetProperty("TargetNodeId", out var t) ? t.GetString() : null;
                    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                        return false;
                    edges.Add(new EdgeInput(source, target));
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
