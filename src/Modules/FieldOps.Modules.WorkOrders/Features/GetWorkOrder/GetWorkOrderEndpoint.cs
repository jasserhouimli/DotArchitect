using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.WorkOrders.Features.GetWorkOrder;

public static class GetWorkOrderEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/work-orders/{id:guid}", async (
            GetWorkOrderHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetWorkOrderQuery(id), ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("GetWorkOrder")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200)
        .Produces(404);
    }
}
