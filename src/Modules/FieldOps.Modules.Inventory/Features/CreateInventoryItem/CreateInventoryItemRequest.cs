namespace FieldOps.Modules.Inventory.Features.CreateInventoryItem;

public record CreateInventoryItemRequest(
    string Name,
    string? Description,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    int ReorderLevel,
    string? Supplier
);
