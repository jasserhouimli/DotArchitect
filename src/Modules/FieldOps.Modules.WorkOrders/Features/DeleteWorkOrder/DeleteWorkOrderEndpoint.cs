using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.WorkOrders.Features.DeleteWorkOrder;

public static class DeleteWorkOrderEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/work-orders/{id:guid}", async (
            DeleteWorkOrderHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.NoContent();
        })
        .WithName("DeleteWorkOrder")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(204)
        .Produces(404);
    }
}
