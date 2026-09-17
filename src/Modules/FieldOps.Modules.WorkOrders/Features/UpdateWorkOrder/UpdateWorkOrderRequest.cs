using FieldOps.Modules.WorkOrders.Domain;

namespace FieldOps.Modules.WorkOrders.Features.UpdateWorkOrder;

public record UpdateWorkOrderRequest(
    string? Title,
    string? Description,
    WorkOrderStatus? Status,
    WorkOrderPriority? Priority,
    DateTime? ScheduledDate,
    string? Notes
);
