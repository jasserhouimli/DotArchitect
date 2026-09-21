namespace DotArchitect.Modules.Design.Domain;

public class ProjectReferenceDefinition
{
    public Guid Id { get; set; }
    public Guid DesignId { get; set; }
    public Guid SourceProjectDefinitionId { get; set; }
    public Guid TargetProjectDefinitionId { get; set; }
}
