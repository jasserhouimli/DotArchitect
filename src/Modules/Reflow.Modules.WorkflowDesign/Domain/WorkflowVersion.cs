namespace Reflow.Modules.WorkflowDesign.Domain;

public class WorkflowVersion
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public int VersionNumber { get; set; }
    public string DefinitionJson { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public Guid PublishedBy { get; set; }
}
