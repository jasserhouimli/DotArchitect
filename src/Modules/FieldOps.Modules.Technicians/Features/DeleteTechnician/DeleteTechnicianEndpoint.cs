using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Technicians.Features.DeleteTechnician;

public static class DeleteTechnicianEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/technicians/{id:guid}", [Authorize] async (
            DeleteTechnicianHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.NoContent();
        })
        .WithName("DeleteTechnician")
        .RequireAuthorization()
        .RequireRateLimiting("general")
        .Produces(204)
        .Produces(404);
    }
}
