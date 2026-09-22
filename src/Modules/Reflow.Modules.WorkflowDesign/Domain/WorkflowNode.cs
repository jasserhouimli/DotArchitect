namespace Reflow.Modules.WorkflowDesign.Domain;

public class WorkflowNode
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string? ConfigJson { get; set; }
    public string? Label { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
