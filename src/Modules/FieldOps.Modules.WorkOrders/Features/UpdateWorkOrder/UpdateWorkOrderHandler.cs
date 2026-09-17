using FieldOps.Infrastructure.Results;
using FieldOps.Modules.WorkOrders.Domain;
using FieldOps.Modules.WorkOrders.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.WorkOrders.Features.UpdateWorkOrder;

public class UpdateWorkOrderHandler(WorkOrdersDbContext db)
{
    public async Task<Result<UpdateWorkOrderResponse>> Handle(
        Guid id,
        UpdateWorkOrderRequest request,
        CancellationToken ct)
    {
        var workOrder = await db.WorkOrders.FindAsync([id], ct);

        if (workOrder is null)
            return Result<UpdateWorkOrderResponse>.Failure("Work order not found", 404);

        if (request.Title is not null)
            workOrder.Title = request.Title;

        if (request.Description is not null)
            workOrder.Description = request.Description;

        if (request.Status.HasValue)
            workOrder.Status = request.Status.Value;

        if (request.Priority.HasValue)
            workOrder.Priority = request.Priority.Value;

        if (request.ScheduledDate.HasValue)
            workOrder.ScheduledDate = request.ScheduledDate.Value;

        if (request.Notes is not null)
            workOrder.Notes = request.Notes;

        workOrder.UpdatedAt = DateTime.UtcNow;

        if (request.Status == WorkOrderStatus.Completed && workOrder.CompletedDate is null)
            workOrder.CompletedDate = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<UpdateWorkOrderResponse>.Success(new UpdateWorkOrderResponse(
            workOrder.Id,
            workOrder.CustomerId,
            workOrder.TechnicianId,
            workOrder.Title,
            workOrder.Description,
            workOrder.Status,
            workOrder.Priority,
            workOrder.ScheduledDate,
            workOrder.CompletedDate,
            workOrder.Notes,
            workOrder.CreatedAt,
            workOrder.UpdatedAt));
    }
}

public record UpdateWorkOrderResponse(
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
