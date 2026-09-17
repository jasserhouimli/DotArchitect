using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Customers.Domain;
using FieldOps.Modules.Customers.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Customers.Features.DeleteCustomer;

public class DeleteCustomerHandler(CustomersDbContext db)
{
    public async Task<Result> Handle(
        Guid id,
        CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (customer is null)
            return Result.Failure("Customer not found", 404);

        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);

        return Result.Success(204);
    }
}
