using DotArchitect.Modules.Workspaces.Domain;
using Microsoft.EntityFrameworkCore;

namespace DotArchitect.Modules.Workspaces.Persistence;

public class WorkspacesDbContext : DbContext
{
    public WorkspacesDbContext(DbContextOptions<WorkspacesDbContext> options) : base(options) { }

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("workspaces");

        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasIndex(e => e.OwnerId);
        });
    }
}
