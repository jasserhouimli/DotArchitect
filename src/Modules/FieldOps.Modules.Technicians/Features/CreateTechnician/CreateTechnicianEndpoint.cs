using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Technicians.Features.CreateTechnician;

public static class CreateTechnicianEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/technicians", async (
            CreateTechnicianHandler handler,
            CreateTechnicianRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("CreateTechnician")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(201)
        .Produces(400)
        .Produces(409);
    }
}
