using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Scheduling.Features.UpdateSchedule;

public static class UpdateScheduleEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPut("/schedules/{id:guid}", async (
            UpdateScheduleHandler handler,
            Guid id,
            UpdateScheduleRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("UpdateSchedule")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200)
        .Produces(404);
    }
}
