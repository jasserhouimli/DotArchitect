namespace DotArchitect.Modules.Design.Domain;

public class ArchitectureGroup
{
    public Guid Id { get; set; }
    public Guid DesignId { get; set; }
    public string Name { get; set; } = string.Empty;
}
