using DotArchitect.Modules.Design.Domain;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Design.Persistence;

public class DesignDbContext : DbContext
{
    public DesignDbContext(DbContextOptions<DesignDbContext> options) : base(options) { }

    public DbSet<SolutionDesign> Designs => Set<SolutionDesign>();
    public DbSet<ProjectDefinition> ProjectDefinitions => Set<ProjectDefinition>();
    public DbSet<ProjectReferenceDefinition> ProjectReferenceDefinitions => Set<ProjectReferenceDefinition>();
    public DbSet<ArchitectureGroup> ArchitectureGroups => Set<ArchitectureGroup>();
    public DbSet<DesignNodeLayout> DesignNodeLayouts => Set<DesignNodeLayout>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("design");

        modelBuilder.Entity<SolutionDesign>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.WorkspaceId);
        });

        modelBuilder.Entity<ProjectDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RelativePath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.TemplateType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TargetFramework).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => new { e.DesignId, e.Name }).IsUnique();
            entity.HasIndex(e => new { e.DesignId, e.RelativePath }).IsUnique();
        });

        modelBuilder.Entity<ProjectReferenceDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DesignId, e.SourceProjectDefinitionId, e.TargetProjectDefinitionId }).IsUnique();
        });

        modelBuilder.Entity<ArchitectureGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => new { e.DesignId, e.Name }).IsUnique();
        });

        modelBuilder.Entity<DesignNodeLayout>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DesignId, e.ProjectDefinitionId }).IsUnique();
        });
    }
}
