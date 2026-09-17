using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Modules.Customers.Features.GetCustomers;

public static class GetCustomersEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/customers", [Authorize] async (
            GetCustomersHandler handler,
            int page = 1,
            int pageSize = 10,
            string? search = null,
            CancellationToken ct = default) =>
        {
            var result = await handler.Handle(new GetCustomersQuery(page, pageSize, search), ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Ok(result.Value);
        })
        .WithName("GetCustomers")
        .RequireAuthorization()
        .Produces(200);
    }
}
