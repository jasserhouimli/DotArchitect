using FieldOps.Modules.WorkOrders.Domain;

namespace FieldOps.Modules.WorkOrders.Features.GetWorkOrders;

public record GetWorkOrdersQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? CustomerId = null,
    Guid? TechnicianId = null,
    WorkOrderStatus? Status = null,
    WorkOrderPriority? Priority = null,
    string? Search = null
);
