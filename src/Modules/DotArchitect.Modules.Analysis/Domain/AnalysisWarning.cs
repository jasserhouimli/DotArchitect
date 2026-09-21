namespace DotArchitect.Modules.Analysis.Domain;

public class AnalysisWarning
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelativePath { get; set; }
    public DateTime CreatedAt { get; set; }
}
