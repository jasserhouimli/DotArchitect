using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Inventory.Domain;
using FieldOps.Modules.Inventory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Inventory.Features.RecordTransaction;

public class RecordTransactionHandler(InventoryDbContext db)
{
    public async Task<Result<RecordTransactionResponse>> Handle(
        RecordTransactionRequest request,
        CancellationToken ct)
    {
        var item = await db.InventoryItems.FindAsync([request.InventoryItemId], ct);

        if (item is null)
            return Result<RecordTransactionResponse>.Failure("Inventory item not found", 404);

        if (request.TransactionType == TransactionType.Used && item.Quantity < request.Quantity)
            return Result<RecordTransactionResponse>.Failure("Insufficient stock", 400);

        var transaction = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            InventoryItemId = request.InventoryItemId,
            WorkOrderId = request.WorkOrderId,
            TransactionType = request.TransactionType,
            Quantity = request.Quantity,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        item.Quantity = request.TransactionType switch
        {
            TransactionType.Received => item.Quantity + request.Quantity,
            TransactionType.Used => item.Quantity - request.Quantity,
            TransactionType.Adjusted => item.Quantity + request.Quantity,
            TransactionType.Returned => item.Quantity + request.Quantity,
            _ => item.Quantity
        };

        item.UpdatedAt = DateTime.UtcNow;

        db.InventoryTransactions.Add(transaction);
        await db.SaveChangesAsync(ct);

        return Result<RecordTransactionResponse>.Success(new RecordTransactionResponse(
            transaction.Id,
            transaction.InventoryItemId,
            transaction.WorkOrderId,
            transaction.TransactionType,
            transaction.Quantity,
            item.Quantity,
            transaction.Notes,
            transaction.CreatedAt), 201);
    }
}

public record RecordTransactionResponse(
    Guid Id,
    Guid InventoryItemId,
    Guid? WorkOrderId,
    TransactionType TransactionType,
    int Quantity,
    int NewStockLevel,
    string? Notes,
    DateTime CreatedAt
);
