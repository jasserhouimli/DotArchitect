using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Inventory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Inventory.Features.DeleteInventoryItem;

public class DeleteInventoryItemHandler(InventoryDbContext db)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        var item = await db.InventoryItems.FindAsync([id], ct);

        if (item is null)
            return Result.Failure("Inventory item not found", 404);

        db.InventoryItems.Remove(item);
        await db.SaveChangesAsync(ct);

        return Result.Success(204);
    }
}
