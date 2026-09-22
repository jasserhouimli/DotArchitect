using System.Text.Json;
using Reflow.Infrastructure.Results;
using Reflow.Modules.WorkflowDesign.Domain;
using Reflow.Modules.WorkflowDesign.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Features.PublishWorkflow;

public class PublishWorkflowHandler(WorkflowDesignDbContext db)
{
    public async Task<Result<int>> Handle(Guid workflowId, Guid ownerId, CancellationToken ct)
    {
        var workflow = await db.Workflows
            .Include(w => w.Nodes)
            .Include(w => w.Edges)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.OwnerId == ownerId, ct);

        if (workflow is null)
            return Result<int>.Failure("Workflow not found", 404);

        if (workflow.Nodes.Count == 0)
            return Result<int>.Failure("Cannot publish a workflow with no nodes", 400);

        var hasCycle = DetectCycle(workflow.Nodes.Select(n => n.NodeId).ToList(), workflow.Edges);
        if (hasCycle)
            return Result<int>.Failure("Workflow graph contains a cycle", 400);

        var versionNumber = workflow.CurrentVersion + 1;
        var definition = JsonSerializer.Serialize(new
        {
            workflow.Name,
            Nodes = workflow.Nodes.Select(n => new { n.NodeId, n.NodeType, n.ConfigJson, n.Label, n.PositionX, n.PositionY }),
            Edges = workflow.Edges.Select(e => new { e.SourceNodeId, e.TargetNodeId })
        });

        var version = new WorkflowVersion
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            VersionNumber = versionNumber,
            DefinitionJson = definition,
            PublishedAt = DateTime.UtcNow,
            PublishedBy = ownerId
        };

        workflow.CurrentVersion = versionNumber;
        workflow.Status = WorkflowStatus.Published;
        workflow.UpdatedAt = DateTime.UtcNow;

        db.WorkflowVersions.Add(version);
        await db.SaveChangesAsync(ct);

        return Result<int>.Success(versionNumber, 200);
    }

    private static bool DetectCycle(List<string> nodeIds, List<WorkflowEdge> edges)
    {
        var adj = new Dictionary<string, List<string>>();
        foreach (var id in nodeIds) adj[id] = new List<string>();
        foreach (var e in edges)
        {
            if (adj.ContainsKey(e.SourceNodeId))
                adj[e.SourceNodeId].Add(e.TargetNodeId);
        }

        var visited = new HashSet<string>();
        var stack = new HashSet<string>();

        bool Dfs(string node)
        {
            visited.Add(node);
            stack.Add(node);
            if (adj.TryGetValue(node, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        if (Dfs(neighbor)) return true;
                    }
                    else if (stack.Contains(neighbor))
                    {
                        return true;
                    }
                }
            }
            stack.Remove(node);
            return false;
        }

        foreach (var id in nodeIds)
        {
            if (!visited.Contains(id) && Dfs(id))
                return true;
        }
        return false;
    }
}
