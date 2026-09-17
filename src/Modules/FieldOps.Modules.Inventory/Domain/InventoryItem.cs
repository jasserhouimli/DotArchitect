namespace FieldOps.Modules.Inventory.Domain;

public class InventoryItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int ReorderLevel { get; set; }
    public string? Supplier { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class InventoryTransaction
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public TransactionType TransactionType { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum TransactionType
{
    Received = 0,
    Used = 1,
    Adjusted = 2,
    Returned = 3
}
