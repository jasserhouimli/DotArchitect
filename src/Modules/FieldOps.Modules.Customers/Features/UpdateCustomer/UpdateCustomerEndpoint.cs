using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Customers.Features.UpdateCustomer;

public static class UpdateCustomerEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPut("/customers/{id:guid}", [Authorize] async (
            UpdateCustomerHandler handler,
            Guid id,
            UpdateCustomerRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Ok(result.Value);
        })
        .WithName("UpdateCustomer")
        .RequireAuthorization()
        .RequireRateLimiting("general")
        .Produces(200)
        .Produces(404);
    }
}
