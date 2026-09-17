using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Invoicing.Domain;
using FieldOps.Modules.Invoicing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Invoicing.Features.AddInvoiceItem;

public class AddInvoiceItemHandler(InvoicingDbContext db)
{
    public async Task<Result<AddInvoiceItemResponse>> Handle(
        Guid invoiceId,
        AddInvoiceItemRequest request,
        CancellationToken ct)
    {
        var invoice = await db.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice is null)
            return Result<AddInvoiceItemResponse>.Failure("Invoice not found", 404);

        if (invoice.Status != InvoiceStatus.Draft)
            return Result<AddInvoiceItemResponse>.Failure("Can only add items to draft invoices", 400);

        var total = request.Quantity * request.UnitPrice;

        var item = new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            Description = request.Description,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            Total = total
        };

        invoice.Items.Add(item);

        invoice.SubTotal = invoice.Items.Sum(i => i.Total);
        invoice.TaxAmount = invoice.SubTotal * invoice.TaxRate / 100;
        invoice.Total = invoice.SubTotal + invoice.TaxAmount;
        invoice.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<AddInvoiceItemResponse>.Success(new AddInvoiceItemResponse(
            item.Id, item.InvoiceId, item.Description,
            item.Quantity, item.UnitPrice, item.Total,
            invoice.SubTotal, invoice.Total), 201);
    }
}

public record AddInvoiceItemResponse(
    Guid Id, Guid InvoiceId, string Description,
    int Quantity, decimal UnitPrice, decimal Total,
    decimal InvoiceSubTotal, decimal InvoiceTotal
);
