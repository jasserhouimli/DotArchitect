using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Invoicing.Features.DeleteInvoice;

public static class DeleteInvoiceEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/invoices/{id:guid}", async (
            DeleteInvoiceHandler handler,
            Guid id,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.NoContent();
        })
        .WithName("DeleteInvoice")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(204)
        .Produces(404);
    }
}
