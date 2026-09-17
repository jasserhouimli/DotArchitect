using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Inventory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Inventory.Features.GetInventoryItems;

public class GetInventoryItemsHandler(InventoryDbContext db)
{
    public async Task<Result<GetInventoryItemsResponse>> Handle(
        GetInventoryItemsQuery query,
        CancellationToken ct)
    {
        var queryable = db.InventoryItems.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
            queryable = queryable.Where(i => i.Name.Contains(query.Search) || i.Sku.Contains(query.Search));

        if (query.LowStock == true)
            queryable = queryable.Where(i => i.Quantity <= i.ReorderLevel);

        var totalCount = await queryable.CountAsync(ct);

        var items = await queryable
            .OrderBy(i => i.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(i => new InventoryItemDto(
                i.Id, i.Name, i.Description, i.Sku,
                i.Quantity, i.UnitPrice, i.ReorderLevel,
                i.Supplier, i.CreatedAt, i.UpdatedAt))
            .ToListAsync(ct);

        return Result<GetInventoryItemsResponse>.Success(new GetInventoryItemsResponse(
            items, totalCount, query.Page, query.PageSize));
    }
}

public record GetInventoryItemsResponse(
    List<InventoryItemDto> Items, int TotalCount, int Page, int PageSize
);

public record InventoryItemDto(
    Guid Id, string Name, string? Description, string Sku,
    int Quantity, decimal UnitPrice, int ReorderLevel,
    string? Supplier, DateTime CreatedAt, DateTime UpdatedAt
);
