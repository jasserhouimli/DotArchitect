using FieldOps.Infrastructure.Results;
using FieldOps.Modules.WorkOrders.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.WorkOrders.Features.DeleteWorkOrder;

public class DeleteWorkOrderHandler(WorkOrdersDbContext db)
{
    public async Task<Result> Handle(
        Guid id,
        CancellationToken ct)
    {
        var workOrder = await db.WorkOrders.FindAsync([id], ct);

        if (workOrder is null)
            return Result.Failure("Work order not found", 404);

        db.WorkOrders.Remove(workOrder);
        await db.SaveChangesAsync(ct);

        return Result.Success(204);
    }
}
