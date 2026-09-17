namespace FieldOps.Modules.Inventory.Features.GetInventoryItems;

public record GetInventoryItemsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? LowStock = null
);
