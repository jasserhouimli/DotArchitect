using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Scheduling.Features.DeleteSchedule;

public static class DeleteScheduleEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/schedules/{id:guid}", async (
            DeleteScheduleHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.NoContent();
        })
        .WithName("DeleteSchedule")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(204)
        .Produces(404);
    }
}
