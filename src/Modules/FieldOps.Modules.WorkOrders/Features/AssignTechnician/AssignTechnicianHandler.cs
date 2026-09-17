using FieldOps.Infrastructure.Results;
using FieldOps.Modules.WorkOrders.Domain;
using FieldOps.Modules.WorkOrders.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.WorkOrders.Features.AssignTechnician;

public class AssignTechnicianHandler(WorkOrdersDbContext db)
{
    public async Task<Result<AssignTechnicianResponse>> Handle(
        Guid workOrderId,
        AssignTechnicianRequest request,
        CancellationToken ct)
    {
        var workOrder = await db.WorkOrders.FindAsync([workOrderId], ct);

        if (workOrder is null)
            return Result<AssignTechnicianResponse>.Failure("Work order not found", 404);

        workOrder.TechnicianId = request.TechnicianId;
        workOrder.Status = WorkOrderStatus.Scheduled;
        workOrder.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<AssignTechnicianResponse>.Success(new AssignTechnicianResponse(
            workOrder.Id,
            workOrder.CustomerId,
            workOrder.TechnicianId,
            workOrder.Title,
            workOrder.Status,
            workOrder.UpdatedAt));
    }
}

public record AssignTechnicianResponse(
    Guid Id,
    Guid CustomerId,
    Guid? TechnicianId,
    string Title,
    WorkOrderStatus Status,
    DateTime UpdatedAt
);
