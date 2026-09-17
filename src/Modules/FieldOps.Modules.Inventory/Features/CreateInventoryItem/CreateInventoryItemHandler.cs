using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Inventory.Domain;
using FieldOps.Modules.Inventory.Persistence;

namespace FieldOps.Modules.Inventory.Features.CreateInventoryItem;

public class CreateInventoryItemHandler(InventoryDbContext db)
{
    public async Task<Result<CreateInventoryItemResponse>> Handle(
        CreateInventoryItemRequest request,
        CancellationToken ct)
    {
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Sku = request.Sku,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            ReorderLevel = request.ReorderLevel,
            Supplier = request.Supplier,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.InventoryItems.Add(item);
        await db.SaveChangesAsync(ct);

        return Result<CreateInventoryItemResponse>.Success(new CreateInventoryItemResponse(
            item.Id, item.Name, item.Description, item.Sku,
            item.Quantity, item.UnitPrice, item.ReorderLevel,
            item.Supplier, item.CreatedAt), 201);
    }
}

public record CreateInventoryItemResponse(
    Guid Id, string Name, string? Description, string Sku,
    int Quantity, decimal UnitPrice, int ReorderLevel,
    string? Supplier, DateTime CreatedAt
);
