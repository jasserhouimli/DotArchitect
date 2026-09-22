namespace Reflow.Modules.WorkflowDesign.Domain;

public class WorkflowEdge
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
