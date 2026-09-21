namespace DotArchitect.Modules.Design.Domain;

public class ProjectDefinition
{
    public Guid Id { get; set; }
    public Guid DesignId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = "net10.0";
}
