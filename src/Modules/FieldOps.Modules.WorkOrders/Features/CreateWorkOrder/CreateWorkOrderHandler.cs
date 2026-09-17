using FieldOps.Infrastructure.Results;
using FieldOps.Modules.WorkOrders.Domain;
using FieldOps.Modules.WorkOrders.Persistence;

namespace FieldOps.Modules.WorkOrders.Features.CreateWorkOrder;

public class CreateWorkOrderHandler(WorkOrdersDbContext db)
{
    public async Task<Result<CreateWorkOrderResponse>> Handle(
        CreateWorkOrderRequest request,
        CancellationToken ct)
    {
        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            ScheduledDate = request.ScheduledDate,
            Notes = request.Notes,
            Status = WorkOrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.WorkOrders.Add(workOrder);
        await db.SaveChangesAsync(ct);

        return Result<CreateWorkOrderResponse>.Success(new CreateWorkOrderResponse(
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
            workOrder.CreatedAt), 201);
    }
}

public record CreateWorkOrderResponse(
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
    DateTime CreatedAt
);
