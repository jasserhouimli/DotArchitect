using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Inventory.Features.UpdateInventoryItem;

public static class UpdateInventoryItemEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPut("/inventory/{id:guid}", async (
            UpdateInventoryItemHandler handler,
            Guid id,
            UpdateInventoryItemRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("UpdateInventoryItem")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200)
        .Produces(404);
    }
}
