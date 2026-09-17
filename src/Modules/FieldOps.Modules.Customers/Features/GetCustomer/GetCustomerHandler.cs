using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Customers.Domain;
using FieldOps.Modules.Customers.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Customers.Features.GetCustomer;

public class GetCustomerHandler(CustomersDbContext db)
{
    public async Task<Result<GetCustomerResponse>> Handle(
        GetCustomerQuery query,
        CancellationToken ct)
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Id == query.Id, ct);

        if (customer is null)
            return Result<GetCustomerResponse>.Failure("Customer not found", 404);

        return Result<GetCustomerResponse>.Success(new GetCustomerResponse(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.City,
            customer.State,
            customer.ZipCode,
            customer.Notes,
            customer.CreatedAt,
            customer.UpdatedAt));
    }
}

public record GetCustomerResponse(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
