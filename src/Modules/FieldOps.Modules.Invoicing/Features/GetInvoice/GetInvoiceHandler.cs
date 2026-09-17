using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Invoicing.Domain;
using FieldOps.Modules.Invoicing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Invoicing.Features.GetInvoice;

public class GetInvoiceHandler(InvoicingDbContext db)
{
    public async Task<Result<GetInvoiceResponse>> Handle(
        GetInvoiceQuery query,
        CancellationToken ct)
    {
        var invoice = await db.Invoices
            .Include(i => i.Items)
            .Where(i => i.Id == query.Id)
            .Select(i => new GetInvoiceResponse(
                i.Id, i.CustomerId, i.WorkOrderId,
                i.InvoiceNumber, i.Status, i.SubTotal,
                i.TaxRate, i.TaxAmount, i.Total,
                i.DueDate, i.PaidDate, i.Notes,
                i.CreatedAt, i.UpdatedAt,
                i.Items.Select(ii => new InvoiceItemDto(
                    ii.Id, ii.Description, ii.Quantity,
                    ii.UnitPrice, ii.Total)).ToList()))
            .FirstOrDefaultAsync(ct);

        if (invoice is null)
            return Result<GetInvoiceResponse>.Failure("Invoice not found", 404);

        return Result<GetInvoiceResponse>.Success(invoice);
    }
}

public record GetInvoiceResponse(
    Guid Id, Guid CustomerId, Guid? WorkOrderId,
    string InvoiceNumber, InvoiceStatus Status, decimal SubTotal,
    decimal TaxRate, decimal TaxAmount, decimal Total,
    DateTime? DueDate, DateTime? PaidDate, string? Notes,
    DateTime CreatedAt, DateTime UpdatedAt,
    List<InvoiceItemDto> Items
);

public record InvoiceItemDto(
    Guid Id, string Description, int Quantity,
    decimal UnitPrice, decimal Total
);
