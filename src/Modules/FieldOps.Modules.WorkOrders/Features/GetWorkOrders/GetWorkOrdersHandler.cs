using FieldOps.Infrastructure.Results;
using FieldOps.Modules.WorkOrders.Domain;
using FieldOps.Modules.WorkOrders.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.WorkOrders.Features.GetWorkOrders;

public class GetWorkOrdersHandler(WorkOrdersDbContext db)
{
    public async Task<Result<GetWorkOrdersResponse>> Handle(
        GetWorkOrdersQuery query,
        CancellationToken ct)
    {
        var queryable = db.WorkOrders.AsQueryable();

        if (query.CustomerId.HasValue)
            queryable = queryable.Where(w => w.CustomerId == query.CustomerId.Value);

        if (query.TechnicianId.HasValue)
            queryable = queryable.Where(w => w.TechnicianId == query.TechnicianId.Value);

        if (query.Status.HasValue)
            queryable = queryable.Where(w => w.Status == query.Status.Value);

        if (query.Priority.HasValue)
            queryable = queryable.Where(w => w.Priority == query.Priority.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
            queryable = queryable.Where(w => w.Title.Contains(query.Search) || (w.Description != null && w.Description.Contains(query.Search)));

        var totalCount = await queryable.CountAsync(ct);

        var items = await queryable
            .OrderByDescending(w => w.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(w => new WorkOrderDto(
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
            .ToListAsync(ct);

        return Result<GetWorkOrdersResponse>.Success(new GetWorkOrdersResponse(
            items,
            totalCount,
            query.Page,
            query.PageSize));
    }
}

public record GetWorkOrdersResponse(
    List<WorkOrderDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public record WorkOrderDto(
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
