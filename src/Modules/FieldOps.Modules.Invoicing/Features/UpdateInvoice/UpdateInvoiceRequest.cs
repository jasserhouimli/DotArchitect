using FieldOps.Modules.Invoicing.Domain;

namespace FieldOps.Modules.Invoicing.Features.UpdateInvoice;

public record UpdateInvoiceRequest(
    InvoiceStatus? Status,
    DateTime? DueDate,
    decimal? TaxRate,
    string? Notes
);
