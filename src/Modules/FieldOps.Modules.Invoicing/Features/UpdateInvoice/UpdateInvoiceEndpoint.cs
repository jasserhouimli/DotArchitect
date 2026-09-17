using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Invoicing.Features.UpdateInvoice;

public static class UpdateInvoiceEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPut("/invoices/{id:guid}", async (
            UpdateInvoiceHandler handler,
            Guid id,
            UpdateInvoiceRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("UpdateInvoice")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(200)
        .Produces(404);
    }
}
