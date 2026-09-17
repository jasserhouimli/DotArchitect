using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Inventory.Features.CreateInventoryItem;

public static class CreateInventoryItemEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/inventory", async (
            CreateInventoryItemHandler handler,
            CreateInventoryItemRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("CreateInventoryItem")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(201)
        .Produces(400);
    }
}
