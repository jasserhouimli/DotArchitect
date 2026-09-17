using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Customers.Domain;
using FieldOps.Modules.Customers.Persistence;

namespace FieldOps.Modules.Customers.Features.CreateCustomer;

public class CreateCustomerHandler(CustomersDbContext db)
{
    public async Task<Result<CreateCustomerResponse>> Handle(
        CreateCustomerRequest request,
        CancellationToken ct)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            City = request.City,
            State = request.State,
            ZipCode = request.ZipCode,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return Result<CreateCustomerResponse>.Success(new CreateCustomerResponse(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.City,
            customer.State,
            customer.ZipCode,
            customer.Notes,
            customer.CreatedAt), 201);
    }
}

public record CreateCustomerResponse(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? ZipCode,
    string? Notes,
    DateTime CreatedAt
);
