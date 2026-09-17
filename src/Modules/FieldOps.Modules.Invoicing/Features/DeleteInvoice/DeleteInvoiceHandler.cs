using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Invoicing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Invoicing.Features.DeleteInvoice;

public class DeleteInvoiceHandler(InvoicingDbContext db)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        var invoice = await db.Invoices.FindAsync([id], ct);

        if (invoice is null)
            return Result.Failure("Invoice not found", 404);

        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(ct);

        return Result.Success(204);
    }
}
