using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Technicians.Features.AddTechnicianSkill;

public static class AddTechnicianSkillEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/technicians/{id:guid}/skills", [Authorize] async (
            AddTechnicianSkillHandler handler,
            Guid id,
            AddTechnicianSkillRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("AddTechnicianSkill")
        .RequireAuthorization()
        .RequireRateLimiting("general")
        .Produces(201)
        .Produces(404)
        .Produces(409);
    }
}
