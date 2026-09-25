using Reflow.Modules.Triggers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.Triggers.Persistence;

public class TriggersDbContext : DbContext
{
    public TriggersDbContext(DbContextOptions<TriggersDbContext> options) : base(options) { }

    public DbSet<Trigger> Triggers => Set<Trigger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("triggers");

        modelBuilder.Entity<Trigger>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.WorkflowId, x.Kind });
            e.HasIndex(x => new { x.Kind, x.IsEnabled, x.NextRunAt });
            e.HasIndex(x => x.SecretTokenHash);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.CronExpression).HasMaxLength(100);
            e.Property(x => x.Timezone).HasMaxLength(100);
            e.Property(x => x.SecretTokenHash).HasMaxLength(64);
        });
    }
}
