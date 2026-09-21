namespace DotArchitect.Modules.Analysis.Domain;

public enum AnalysisStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public class Analysis
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public AnalysisStatus Status { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProjectCount { get; set; }
    public int ReferenceCount { get; set; }
    public int WarningCount { get; set; }
    public string? ErrorCode { get; set; }
}
