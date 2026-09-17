using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Technicians.Features.UpdateTechnician;

public static class UpdateTechnicianEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPut("/technicians/{id:guid}", [Authorize] async (
            UpdateTechnicianHandler handler,
            Guid id,
            UpdateTechnicianRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Ok(result.Value);
        })
        .WithName("UpdateTechnician")
        .RequireAuthorization()
        .RequireRateLimiting("general")
        .Produces(200)
        .Produces(404)
        .Produces(409);
    }
}
