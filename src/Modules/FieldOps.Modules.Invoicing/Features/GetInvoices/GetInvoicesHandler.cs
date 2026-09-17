using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Invoicing.Domain;
using FieldOps.Modules.Invoicing.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Invoicing.Features.GetInvoices;

public class GetInvoicesHandler(InvoicingDbContext db)
{
    public async Task<Result<GetInvoicesResponse>> Handle(
        GetInvoicesQuery query,
        CancellationToken ct)
    {
        var queryable = db.Invoices.AsQueryable();

        if (query.CustomerId.HasValue)
            queryable = queryable.Where(i => i.CustomerId == query.CustomerId.Value);

        if (query.Status.HasValue)
            queryable = queryable.Where(i => i.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
            queryable = queryable.Where(i => i.InvoiceNumber.Contains(query.Search));

        var totalCount = await queryable.CountAsync(ct);

        var items = await queryable
            .OrderByDescending(i => i.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(i => new InvoiceDto(
                i.Id, i.CustomerId, i.WorkOrderId,
                i.InvoiceNumber, i.Status, i.SubTotal,
                i.TaxRate, i.TaxAmount, i.Total,
                i.DueDate, i.PaidDate, i.Notes,
                i.CreatedAt, i.UpdatedAt))
            .ToListAsync(ct);

        return Result<GetInvoicesResponse>.Success(new GetInvoicesResponse(
            items, totalCount, query.Page, query.PageSize));
    }
}

public record GetInvoicesResponse(
    List<InvoiceDto> Items, int TotalCount, int Page, int PageSize
);

public record InvoiceDto(
    Guid Id, Guid CustomerId, Guid? WorkOrderId,
    string InvoiceNumber, InvoiceStatus Status, decimal SubTotal,
    decimal TaxRate, decimal TaxAmount, decimal Total,
    DateTime? DueDate, DateTime? PaidDate, string? Notes,
    DateTime CreatedAt, DateTime UpdatedAt
);
