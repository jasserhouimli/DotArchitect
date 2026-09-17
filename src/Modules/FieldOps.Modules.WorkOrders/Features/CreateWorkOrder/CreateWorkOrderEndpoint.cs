using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.WorkOrders.Features.CreateWorkOrder;

public static class CreateWorkOrderEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/work-orders", async (
            CreateWorkOrderHandler handler,
            CreateWorkOrderRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("CreateWorkOrder")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(201)
        .Produces(400);
    }
}
