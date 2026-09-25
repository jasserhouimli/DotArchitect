using Reflow.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Reflow.Modules.Notifications.Persistence;

public class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<NotificationRule> NotificationRules => Set<NotificationRule>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");

        modelBuilder.Entity<NotificationRule>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.WorkflowId).IsUnique();
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.WebhookUrl).HasMaxLength(2000);
        });
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt });
            e.HasIndex(x => new { x.OwnerId, x.IsRead });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Message).HasMaxLength(2000);
        });
    }
}
