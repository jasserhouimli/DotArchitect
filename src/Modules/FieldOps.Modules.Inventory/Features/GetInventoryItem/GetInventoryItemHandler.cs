using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Inventory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Inventory.Features.GetInventoryItem;

public class GetInventoryItemHandler(InventoryDbContext db)
{
    public async Task<Result<GetInventoryItemResponse>> Handle(
        GetInventoryItemQuery query,
        CancellationToken ct)
    {
        var item = await db.InventoryItems
            .Where(i => i.Id == query.Id)
            .Select(i => new GetInventoryItemResponse(
                i.Id, i.Name, i.Description, i.Sku,
                i.Quantity, i.UnitPrice, i.ReorderLevel,
                i.Supplier, i.CreatedAt, i.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        if (item is null)
            return Result<GetInventoryItemResponse>.Failure("Inventory item not found", 404);

        return Result<GetInventoryItemResponse>.Success(item);
    }
}

public record GetInventoryItemResponse(
    Guid Id, string Name, string? Description, string Sku,
    int Quantity, decimal UnitPrice, int ReorderLevel,
    string? Supplier, DateTime CreatedAt, DateTime UpdatedAt
);
