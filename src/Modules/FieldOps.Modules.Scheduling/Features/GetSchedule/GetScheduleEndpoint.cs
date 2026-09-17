using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Scheduling.Features.GetSchedule;

public static class GetScheduleEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/schedules/{id:guid}", async (
            GetScheduleHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetScheduleQuery(id), ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("GetSchedule")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200)
        .Produces(404);
    }
}
