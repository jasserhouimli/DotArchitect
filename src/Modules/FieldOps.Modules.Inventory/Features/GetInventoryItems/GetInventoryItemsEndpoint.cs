using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Inventory.Features.GetInventoryItems;

public static class GetInventoryItemsEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/inventory", async (
            GetInventoryItemsHandler handler,
            int? page,
            int? pageSize,
            string? search,
            bool? lowStock,
            CancellationToken ct) =>
        {
            var query = new GetInventoryItemsQuery(
                page ?? 1, pageSize ?? 20, search, lowStock);

            var result = await handler.Handle(query, ct);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("GetInventoryItems")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200);
    }
}
