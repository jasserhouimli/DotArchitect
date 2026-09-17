using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Inventory.Features.DeleteInventoryItem;

public static class DeleteInventoryItemEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/inventory/{id:guid}", async (
            DeleteInventoryItemHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.NoContent();
        })
        .WithName("DeleteInventoryItem")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(204)
        .Produces(404);
    }
}
