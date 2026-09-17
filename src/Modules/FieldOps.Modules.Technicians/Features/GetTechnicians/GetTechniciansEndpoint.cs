using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Modules.Technicians.Features.GetTechnicians;

public static class GetTechniciansEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/technicians", [Authorize] async (
            GetTechniciansHandler handler,
            int page = 1,
            int pageSize = 10,
            string? search = null,
            bool? isActive = null,
            CancellationToken ct = default) =>
        {
            var result = await handler.Handle(new GetTechniciansQuery(page, pageSize, search, isActive), ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Ok(result.Value);
        })
        .WithName("GetTechnicians")
        .RequireAuthorization()
        .Produces(200);
    }
}
