using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Customers.Domain;
using FieldOps.Modules.Customers.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Customers.Features.UpdateCustomer;

public class UpdateCustomerHandler(CustomersDbContext db)
{
    public async Task<Result<UpdateCustomerResponse>> Handle(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (customer is null)
            return Result<UpdateCustomerResponse>.Failure("Customer not found", 404);

        customer.Name = request.Name;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.Address = request.Address;
        customer.City = request.City;
        customer.State = request.State;
        customer.ZipCode = request.ZipCode;
        customer.Notes = request.Notes;
        customer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<UpdateCustomerResponse>.Success(new UpdateCustomerResponse(
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

public record UpdateCustomerResponse(
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
