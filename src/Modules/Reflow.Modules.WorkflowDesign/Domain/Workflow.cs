namespace Reflow.Modules.WorkflowDesign.Domain;

public enum WorkflowStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

public class Workflow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public int CurrentVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<WorkflowNode> Nodes { get; set; } = new();
    public List<WorkflowEdge> Edges { get; set; } = new();
    public List<WorkflowVersion> Versions { get; set; } = new();
}
