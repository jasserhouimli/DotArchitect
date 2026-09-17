namespace FieldOps.Modules.Inventory.Features.UpdateInventoryItem;

public record UpdateInventoryItemRequest(
    string? Name,
    string? Description,
    string? Sku,
    int? Quantity,
    decimal? UnitPrice,
    int? ReorderLevel,
    string? Supplier
);
