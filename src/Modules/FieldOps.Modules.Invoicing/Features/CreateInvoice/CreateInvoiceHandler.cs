using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Invoicing.Domain;
using FieldOps.Modules.Invoicing.Persistence;

namespace FieldOps.Modules.Invoicing.Features.CreateInvoice;

public class CreateInvoiceHandler(InvoicingDbContext db)
{
    public async Task<Result<CreateInvoiceResponse>> Handle(
        CreateInvoiceRequest request,
        CancellationToken ct)
    {
        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            WorkOrderId = request.WorkOrderId,
            InvoiceNumber = invoiceNumber,
            Status = InvoiceStatus.Draft,
            TaxRate = request.TaxRate,
            DueDate = request.DueDate,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        return Result<CreateInvoiceResponse>.Success(new CreateInvoiceResponse(
            invoice.Id, invoice.CustomerId, invoice.WorkOrderId,
            invoice.InvoiceNumber, invoice.Status, invoice.SubTotal,
            invoice.TaxRate, invoice.TaxAmount, invoice.Total,
            invoice.DueDate, invoice.PaidDate, invoice.Notes,
            invoice.CreatedAt), 201);
    }
}

public record CreateInvoiceResponse(
    Guid Id, Guid CustomerId, Guid? WorkOrderId,
    string InvoiceNumber, InvoiceStatus Status, decimal SubTotal,
    decimal TaxRate, decimal TaxAmount, decimal Total,
    DateTime? DueDate, DateTime? PaidDate, string? Notes,
    DateTime CreatedAt
);
