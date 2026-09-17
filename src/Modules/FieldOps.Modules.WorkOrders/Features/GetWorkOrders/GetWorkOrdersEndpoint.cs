using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.WorkOrders.Features.GetWorkOrders;

public static class GetWorkOrdersEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/work-orders", async (
            GetWorkOrdersHandler handler,
            int? page,
            int? pageSize,
            Guid? customerId,
            Guid? technicianId,
            string? status,
            string? priority,
            string? search,
            CancellationToken ct) =>
        {
            var query = new GetWorkOrdersQuery(
                page ?? 1,
                pageSize ?? 20,
                customerId,
                technicianId,
                Enum.TryParse<WorkOrders.Domain.WorkOrderStatus>(status, true, out var s) ? s : null,
                Enum.TryParse<WorkOrders.Domain.WorkOrderPriority>(priority, true, out var p) ? p : null,
                search);

            var result = await handler.Handle(query, ct);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("GetWorkOrders")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200);
    }
}
