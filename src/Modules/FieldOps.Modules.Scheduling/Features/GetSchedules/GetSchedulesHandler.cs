using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Scheduling.Domain;
using FieldOps.Modules.Scheduling.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Scheduling.Features.GetSchedules;

public class GetSchedulesHandler(SchedulingDbContext db)
{
    public async Task<Result<GetSchedulesResponse>> Handle(
        GetSchedulesQuery query,
        CancellationToken ct)
    {
        var queryable = db.Schedules.AsQueryable();

        if (query.TechnicianId.HasValue)
            queryable = queryable.Where(s => s.TechnicianId == query.TechnicianId.Value);

        if (query.Status.HasValue)
            queryable = queryable.Where(s => s.Status == query.Status.Value);

        if (query.From.HasValue)
            queryable = queryable.Where(s => s.StartTime >= query.From.Value);

        if (query.To.HasValue)
            queryable = queryable.Where(s => s.EndTime <= query.To.Value);

        var totalCount = await queryable.CountAsync(ct);

        var items = await queryable
            .OrderBy(s => s.StartTime)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new ScheduleDto(
                s.Id, s.TechnicianId, s.WorkOrderId,
                s.Title, s.Description, s.StartTime,
                s.EndTime, s.Status, s.Notes,
                s.CreatedAt, s.UpdatedAt))
            .ToListAsync(ct);

        return Result<GetSchedulesResponse>.Success(new GetSchedulesResponse(
            items, totalCount, query.Page, query.PageSize));
    }
}

public record GetSchedulesResponse(
    List<ScheduleDto> Items, int TotalCount, int Page, int PageSize
);

public record ScheduleDto(
    Guid Id, Guid TechnicianId, Guid? WorkOrderId,
    string Title, string? Description, DateTime StartTime,
    DateTime EndTime, ScheduleStatus Status, string? Notes,
    DateTime CreatedAt, DateTime UpdatedAt
);
