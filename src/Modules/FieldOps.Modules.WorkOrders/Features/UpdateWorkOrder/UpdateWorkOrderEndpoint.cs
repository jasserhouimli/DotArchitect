using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.WorkOrders.Features.UpdateWorkOrder;

public static class UpdateWorkOrderEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPut("/work-orders/{id:guid}", async (
            UpdateWorkOrderHandler handler,
            Guid id,
            UpdateWorkOrderRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("UpdateWorkOrder")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200)
        .Produces(404);
    }
}
