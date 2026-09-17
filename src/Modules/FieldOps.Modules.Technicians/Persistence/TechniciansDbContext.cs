using FieldOps.Modules.Technicians.Domain;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Technicians.Persistence;

public class TechniciansDbContext : DbContext
{
    public TechniciansDbContext(DbContextOptions<TechniciansDbContext> options) : base(options) { }

    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<TechnicianSkill> TechnicianSkills => Set<TechnicianSkill>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("technicians");

        modelBuilder.Entity<Technician>(entity =>
        {
            entity.ToTable("technicians");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.HourlyRate).HasColumnType("decimal(18,2)");
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<TechnicianSkill>(entity =>
        {
            entity.ToTable("technician_skills");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SkillName).IsRequired().HasMaxLength(100);
            entity.HasOne<Technician>()
                  .WithMany(t => t.Skills)
                  .HasForeignKey(e => e.TechnicianId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
