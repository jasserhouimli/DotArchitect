using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Inventory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Inventory.Features.UpdateInventoryItem;

public class UpdateInventoryItemHandler(InventoryDbContext db)
{
    public async Task<Result<UpdateInventoryItemResponse>> Handle(
        Guid id,
        UpdateInventoryItemRequest request,
        CancellationToken ct)
    {
        var item = await db.InventoryItems.FindAsync([id], ct);

        if (item is null)
            return Result<UpdateInventoryItemResponse>.Failure("Inventory item not found", 404);

        if (request.Name is not null)
            item.Name = request.Name;
        if (request.Description is not null)
            item.Description = request.Description;
        if (request.Sku is not null)
            item.Sku = request.Sku;
        if (request.Quantity.HasValue)
            item.Quantity = request.Quantity.Value;
        if (request.UnitPrice.HasValue)
            item.UnitPrice = request.UnitPrice.Value;
        if (request.ReorderLevel.HasValue)
            item.ReorderLevel = request.ReorderLevel.Value;
        if (request.Supplier is not null)
            item.Supplier = request.Supplier;

        item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<UpdateInventoryItemResponse>.Success(new UpdateInventoryItemResponse(
            item.Id, item.Name, item.Description, item.Sku,
            item.Quantity, item.UnitPrice, item.ReorderLevel,
            item.Supplier, item.CreatedAt, item.UpdatedAt));
    }
}

public record UpdateInventoryItemResponse(
    Guid Id, string Name, string? Description, string Sku,
    int Quantity, decimal UnitPrice, int ReorderLevel,
    string? Supplier, DateTime CreatedAt, DateTime UpdatedAt
);
