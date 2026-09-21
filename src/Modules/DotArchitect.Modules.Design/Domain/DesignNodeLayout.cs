namespace DotArchitect.Modules.Design.Domain;

public class DesignNodeLayout
{
    public Guid Id { get; set; }
    public Guid DesignId { get; set; }
    public Guid ProjectDefinitionId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}
