using FieldOps.Modules.WorkOrders.Domain;

namespace FieldOps.Modules.WorkOrders.Features.CreateWorkOrder;

public record CreateWorkOrderRequest(
    Guid CustomerId,
    string Title,
    string? Description,
    WorkOrderPriority Priority,
    DateTime? ScheduledDate,
    string? Notes
);
