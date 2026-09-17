using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Scheduling.Features.CreateSchedule;

public static class CreateScheduleEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/schedules", async (
            CreateScheduleHandler handler,
            CreateScheduleRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("CreateSchedule")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(201)
        .Produces(400);
    }
}
