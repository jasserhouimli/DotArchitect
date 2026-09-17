using FieldOps.Modules.WorkOrders.Domain;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.WorkOrders.Persistence;

public class WorkOrdersDbContext : DbContext
{
    public WorkOrdersDbContext(DbContextOptions<WorkOrdersDbContext> options) : base(options) { }

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("work_orders");

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.ToTable("work_orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Priority).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.TechnicianId);
            entity.HasIndex(e => e.Status);
        });
    }
}
