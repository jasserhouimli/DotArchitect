using FieldOps.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Inventory.Persistence;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("inventory");

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("inventory_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Sku).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Supplier).HasMaxLength(256);
            entity.HasIndex(e => e.Sku).IsUnique();
        });

        modelBuilder.Entity<InventoryTransaction>(entity =>
        {
            entity.ToTable("inventory_transactions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TransactionType).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.HasIndex(e => e.InventoryItemId);
            entity.HasOne<InventoryItem>()
                  .WithMany()
                  .HasForeignKey(e => e.InventoryItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
