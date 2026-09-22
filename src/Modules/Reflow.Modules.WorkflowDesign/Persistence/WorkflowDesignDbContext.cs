using Reflow.Modules.WorkflowDesign.Domain;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.WorkflowDesign.Persistence;

public class WorkflowDesignDbContext : DbContext
{
    public WorkflowDesignDbContext(DbContextOptions<WorkflowDesignDbContext> options) : base(options) { }

    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowNode> WorkflowNodes => Set<WorkflowNode>();
    public DbSet<WorkflowEdge> WorkflowEdges => Set<WorkflowEdge>();
    public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("workflow_design");

        modelBuilder.Entity<Workflow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<WorkflowNode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NodeId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.NodeType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ConfigJson).HasMaxLength(10000);
            entity.Property(e => e.Label).HasMaxLength(200);
            entity.HasIndex(e => new { e.WorkflowId, e.NodeId }).IsUnique();
        });

        modelBuilder.Entity<WorkflowEdge>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceNodeId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TargetNodeId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => new { e.WorkflowId, e.SourceNodeId, e.TargetNodeId }).IsUnique();
        });

        modelBuilder.Entity<WorkflowVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DefinitionJson).IsRequired();
            entity.HasIndex(e => new { e.WorkflowId, e.VersionNumber }).IsUnique();
        });
    }
}
