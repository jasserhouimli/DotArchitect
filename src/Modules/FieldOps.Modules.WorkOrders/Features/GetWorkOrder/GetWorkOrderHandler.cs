using FieldOps.Infrastructure.Results;
using FieldOps.Modules.WorkOrders.Domain;
using FieldOps.Modules.WorkOrders.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.WorkOrders.Features.GetWorkOrder;

public class GetWorkOrderHandler(WorkOrdersDbContext db)
{
    public async Task<Result<GetWorkOrderResponse>> Handle(
        GetWorkOrderQuery query,
        CancellationToken ct)
    {
        var workOrder = await db.WorkOrders
            .Where(w => w.Id == query.Id)
            .Select(w => new GetWorkOrderResponse(
                w.Id,
                w.CustomerId,
                w.TechnicianId,
                w.Title,
                w.Description,
                w.Status,
                w.Priority,
                w.ScheduledDate,
                w.CompletedDate,
                w.Notes,
                w.CreatedAt,
                w.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        if (workOrder is null)
            return Result<GetWorkOrderResponse>.Failure("Work order not found", 404);

        return Result<GetWorkOrderResponse>.Success(workOrder);
    }
}

public record GetWorkOrderResponse(
    Guid Id,
    Guid CustomerId,
    Guid? TechnicianId,
    string Title,
    string? Description,
    WorkOrderStatus Status,
    WorkOrderPriority Priority,
    DateTime? ScheduledDate,
    DateTime? CompletedDate,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
