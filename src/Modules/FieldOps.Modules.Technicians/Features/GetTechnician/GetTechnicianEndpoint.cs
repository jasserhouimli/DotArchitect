using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Modules.Technicians.Features.GetTechnician;

public static class GetTechnicianEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/technicians/{id:guid}", [Authorize] async (
            GetTechnicianHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetTechnicianQuery(id), ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Ok(result.Value);
        })
        .WithName("GetTechnician")
        .RequireAuthorization()
        .Produces(200)
        .Produces(404);
    }
}
