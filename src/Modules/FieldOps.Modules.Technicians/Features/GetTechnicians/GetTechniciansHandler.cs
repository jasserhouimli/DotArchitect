using FieldOps.Infrastructure.Results;
using FieldOps.Modules.Technicians.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Modules.Technicians.Features.GetTechnicians;

public class GetTechniciansHandler(TechniciansDbContext db)
{
    public async Task<Result<GetTechniciansResponse>> Handle(
        GetTechniciansQuery query,
        CancellationToken ct)
    {
        var queryable = db.Technicians.AsQueryable();

        if (!string.IsNullOrEmpty(query.Search))
            queryable = queryable.Where(t =>
                t.FirstName.Contains(query.Search) ||
                t.LastName.Contains(query.Search) ||
                t.Email.Contains(query.Search));

        if (query.IsActive.HasValue)
            queryable = queryable.Where(t => t.IsActive == query.IsActive.Value);

        var totalCount = await queryable.CountAsync(ct);

        var technicians = await queryable
            .OrderBy(t => t.LastName)
            .ThenBy(t => t.FirstName)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new TechnicianDto(
                t.Id,
                t.UserId,
                t.FirstName,
                t.LastName,
                t.Email,
                t.Phone,
                t.HourlyRate,
                t.IsActive,
                t.CreatedAt))
            .ToListAsync(ct);

        return Result<GetTechniciansResponse>.Success(new GetTechniciansResponse(
            technicians,
            totalCount,
            query.Page,
            query.PageSize));
    }
}

public record GetTechniciansResponse(
    List<TechnicianDto> Technicians,
    int TotalCount,
    int Page,
    int PageSize
);

public record TechnicianDto(
    Guid Id,
    Guid? UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    decimal HourlyRate,
    bool IsActive,
    DateTime CreatedAt
);
