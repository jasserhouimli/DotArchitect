using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.WorkOrders.Features.AssignTechnician;

public static class AssignTechnicianEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPut("/work-orders/{id:guid}/assign-technician", async (
            AssignTechnicianHandler handler,
            Guid id,
            AssignTechnicianRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("AssignTechnician")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200)
        .Produces(404);
    }
}
