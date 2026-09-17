namespace FieldOps.Modules.Invoicing.Features.CreateInvoice;

public record CreateInvoiceRequest(
    Guid CustomerId,
    Guid? WorkOrderId,
    DateTime? DueDate,
    decimal TaxRate,
    string? Notes
);
