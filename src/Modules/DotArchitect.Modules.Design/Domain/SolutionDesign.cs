namespace DotArchitect.Modules.Design.Domain;

public class SolutionDesign
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Revision { get; set; }
}
