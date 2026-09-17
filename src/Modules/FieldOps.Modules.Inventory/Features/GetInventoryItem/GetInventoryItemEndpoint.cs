using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Inventory.Features.GetInventoryItem;

public static class GetInventoryItemEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/inventory/{id:guid}", async (
            GetInventoryItemHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetInventoryItemQuery(id), ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("GetInventoryItem")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200)
        .Produces(404);
    }
}
