using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Customers.Domain;
using FieldOps.Modules.Customers.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Customers.Features.GetCustomers;

public class GetCustomersHandler(CustomersDbContext db)
{
    public async Task<Result<GetCustomersResponse>> Handle(
        GetCustomersQuery query,
        CancellationToken ct)
    {
        var totalCount = await db.Customers.CountAsync(ct);

        var customers = await db.Customers
            .Where(c => string.IsNullOrEmpty(query.Search) ||
                       c.Name.Contains(query.Search) ||
                       (c.Email != null && c.Email.Contains(query.Search)))
            .OrderBy(c => c.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new CustomerDto(
                c.Id,
                c.Name,
                c.Email,
                c.Phone,
                c.Address,
                c.City,
                c.State,
                c.ZipCode,
                c.CreatedAt))
            .ToListAsync(ct);

        return Result<GetCustomersResponse>.Success(new GetCustomersResponse(
            customers,
            totalCount,
            query.Page,
            query.PageSize));
    }
}

public record GetCustomersResponse(
    List<CustomerDto> Customers,
    int TotalCount,
    int Page,
    int PageSize
);

public record CustomerDto(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    DateTime CreatedAt
);
