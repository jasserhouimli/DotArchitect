namespace DotArchitect.Modules.Analysis.Domain;

public class ProjectReference
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }
    public Guid SourceProjectId { get; set; }
    public Guid TargetProjectId { get; set; }
}
