using Reflow.Infrastructure.Results;

namespace Reflow.Infrastructure.Snapshots;

public record SnapshotNode(string NodeId, string NodeType, string? ConfigJson);

public record SnapshotEdge(string SourceNodeId, string TargetNodeId);

public record WorkflowSnapshot(
    Guid WorkflowId,
    string Name,
    int VersionNumber,
    Guid VersionId,
    List<SnapshotNode> Nodes,
    List<SnapshotEdge> Edges);

public interface IWorkflowSnapshotProvider
{
    Task<Result<WorkflowSnapshot>> GetPublishedSnapshotAsync(Guid workflowId, Guid ownerId, CancellationToken ct);
}
