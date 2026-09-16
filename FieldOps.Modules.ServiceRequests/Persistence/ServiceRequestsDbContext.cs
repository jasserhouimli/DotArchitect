using FieldOps.Modules.ServiceRequests.Domain;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.ServiceRequests.Persistence;

public class ServiceRequestsDbContext : DbContext
{
    public ServiceRequestsDbContext(DbContextOptions<ServiceRequestsDbContext> options) : base(options) { }

    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Priority).HasMaxLength(20);
        });
    }
}
