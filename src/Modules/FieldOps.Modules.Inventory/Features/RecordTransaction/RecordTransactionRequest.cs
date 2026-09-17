using FieldOps.Modules.Inventory.Domain;

namespace FieldOps.Modules.Inventory.Features.RecordTransaction;

public record RecordTransactionRequest(
    Guid InventoryItemId,
    Guid? WorkOrderId,
    TransactionType TransactionType,
    int Quantity,
    string? Notes
);
