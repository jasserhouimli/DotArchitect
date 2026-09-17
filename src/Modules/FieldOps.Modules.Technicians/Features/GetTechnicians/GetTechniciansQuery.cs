namespace FieldOps.Modules.Technicians.Features.GetTechnicians;

public record GetTechniciansQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    bool? IsActive = null
);
