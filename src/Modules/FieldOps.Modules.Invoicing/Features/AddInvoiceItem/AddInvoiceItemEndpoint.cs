using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Modules.Invoicing.Features.AddInvoiceItem;

public static class AddInvoiceItemEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/invoices/{id:guid}/items", async (
            AddInvoiceItemHandler handler,
            Guid id,
            AddInvoiceItemRequest request,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(id, request, ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            return Results.Json(result.Value, statusCode: result.StatusCode);
        })
        .WithName("AddInvoiceItem")
        .RequireRateLimiting("general")
        .RequireAuthorization()
        .Produces(201)
        .Produces(400);
    }
}
