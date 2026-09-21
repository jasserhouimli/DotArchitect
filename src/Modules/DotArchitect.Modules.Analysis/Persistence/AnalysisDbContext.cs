using DotArchitect.Modules.Analysis.Domain;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Analysis.Persistence;

public class AnalysisDbContext : DbContext
{
    public AnalysisDbContext(DbContextOptions<AnalysisDbContext> options) : base(options) { }

    public DbSet<Domain.Analysis> Analyses => Set<Domain.Analysis>();
    public DbSet<AnalyzedProject> AnalyzedProjects => Set<AnalyzedProject>();
    public DbSet<ProjectReference> ProjectReferences => Set<ProjectReference>();
    public DbSet<AnalysisWarning> AnalysisWarnings => Set<AnalysisWarning>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("analysis");

        modelBuilder.Entity<Domain.Analysis>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ErrorCode).HasMaxLength(200);
            entity.HasIndex(e => e.WorkspaceId);
            entity.HasIndex(e => new { e.WorkspaceId, e.StartedAt });
        });

        modelBuilder.Entity<AnalyzedProject>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RelativePath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ProjectType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TargetFrameworks).HasMaxLength(500);
            entity.HasIndex(e => new { e.AnalysisId, e.RelativePath }).IsUnique();
            entity.HasIndex(e => new { e.AnalysisId, e.Name });
        });

        modelBuilder.Entity<ProjectReference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AnalysisId, e.SourceProjectId, e.TargetProjectId }).IsUnique();
            entity.HasIndex(e => new { e.AnalysisId, e.SourceProjectId });
            entity.HasIndex(e => new { e.AnalysisId, e.TargetProjectId });
        });

        modelBuilder.Entity<AnalysisWarning>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.RelativePath).HasMaxLength(1000);
            entity.HasIndex(e => new { e.AnalysisId, e.Code });
        });
    }
}
