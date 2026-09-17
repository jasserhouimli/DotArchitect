namespace FieldOps.Modules.Invoicing.Features.AddInvoiceItem;

public record AddInvoiceItemRequest(
    string Description,
    int Quantity,
    decimal UnitPrice
);
