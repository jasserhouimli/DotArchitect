using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Invoicing.Features.GetInvoice;

public static class GetInvoiceEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/invoices/{id:guid}", async (
            GetInvoiceHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetInvoiceQuery(id), ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("GetInvoice")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200)
        .Produces(404);
    }
}
