using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using FieldOps.Modules.Scheduling.Domain;

namespace FieldOps.Modules.Scheduling.Features.GetSchedules;

public static class GetSchedulesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/schedules", async (
            GetSchedulesHandler handler,
            int? page,
            int? pageSize,
            Guid? technicianId,
            string? status,
            DateTime? from,
            DateTime? to,
            CancellationToken ct) =>
        {
            var query = new GetSchedulesQuery(
                page ?? 1, pageSize ?? 20,
                technicianId,
                Enum.TryParse<ScheduleStatus>(status, true, out var s) ? s : null,
                from, to);

            var result = await handler.Handle(query, ct);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("GetSchedules")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200);
    }
}
