using FieldOps.Modules.Invoicing.Domain;

namespace FieldOps.Modules.Invoicing.Features.GetInvoices;

public record GetInvoicesQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? CustomerId = null,
    InvoiceStatus? Status = null,
    string? Search = null
);
