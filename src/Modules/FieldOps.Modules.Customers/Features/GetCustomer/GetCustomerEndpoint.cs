using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Modules.Customers.Features.GetCustomer;

public static class GetCustomerEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/customers/{id:guid}", [Authorize] async (
            GetCustomerHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetCustomerQuery(id), ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Ok(result.Value);
        })
        .WithName("GetCustomer")
        .RequireAuthorization()
        .Produces(200)
        .Produces(404);
    }
}
