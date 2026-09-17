using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Customers.Features.DeleteCustomer;

public static class DeleteCustomerEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/customers/{id:guid}", [Authorize] async (
            DeleteCustomerHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.NoContent();
        })
        .WithName("DeleteCustomer")
        .RequireAuthorization()
        .RequireRateLimiting("general")
        .Produces(204)
        .Produces(404);
    }
}
