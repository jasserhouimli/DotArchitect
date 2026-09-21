namespace DotArchitect.Modules.Analysis.Domain;

public class AnalyzedProject
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string ProjectType { get; set; } = string.Empty;
    public string TargetFrameworks { get; set; } = string.Empty;
}
