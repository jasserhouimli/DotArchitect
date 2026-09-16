using FieldOps.Modules.Technicians.Domain;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Technicians.Persistence;

public class TechniciansDbContext : DbContext
{
    public TechniciansDbContext(DbContextOptions<TechniciansDbContext> options) : base(options) { }

    public DbSet<Technician> Technicians => Set<Technician>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Technician>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Specialty).HasMaxLength(100);
        });
    }
}
