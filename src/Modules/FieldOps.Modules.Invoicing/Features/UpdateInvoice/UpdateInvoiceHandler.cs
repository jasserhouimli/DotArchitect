using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Invoicing.Domain;
using FieldOps.Modules.Invoicing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Invoicing.Features.UpdateInvoice;

public class UpdateInvoiceHandler(InvoicingDbContext db)
{
    public async Task<Result<UpdateInvoiceResponse>> Handle(
        Guid id,
        UpdateInvoiceRequest request,
        CancellationToken ct)
    {
        var invoice = await db.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice is null)
            return Result<UpdateInvoiceResponse>.Failure("Invoice not found", 404);

        if (request.Status.HasValue)
        {
            invoice.Status = request.Status.Value;
            if (request.Status == InvoiceStatus.Paid && invoice.PaidDate is null)
                invoice.PaidDate = DateTime.UtcNow;
        }

        if (request.DueDate.HasValue)
            invoice.DueDate = request.DueDate.Value;

        if (request.TaxRate.HasValue)
        {
            invoice.TaxRate = request.TaxRate.Value;
            invoice.TaxAmount = invoice.SubTotal * request.TaxRate.Value / 100;
            invoice.Total = invoice.SubTotal + invoice.TaxAmount;
        }

        if (request.Notes is not null)
            invoice.Notes = request.Notes;

        invoice.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<UpdateInvoiceResponse>.Success(new UpdateInvoiceResponse(
            invoice.Id, invoice.CustomerId, invoice.WorkOrderId,
            invoice.InvoiceNumber, invoice.Status, invoice.SubTotal,
            invoice.TaxRate, invoice.TaxAmount, invoice.Total,
            invoice.DueDate, invoice.PaidDate, invoice.Notes,
            invoice.CreatedAt, invoice.UpdatedAt));
    }
}

public record UpdateInvoiceResponse(
    Guid Id, Guid CustomerId, Guid? WorkOrderId,
    string InvoiceNumber, InvoiceStatus Status, decimal SubTotal,
    decimal TaxRate, decimal TaxAmount, decimal Total,
    DateTime? DueDate, DateTime? PaidDate, string? Notes,
    DateTime CreatedAt, DateTime UpdatedAt
);
